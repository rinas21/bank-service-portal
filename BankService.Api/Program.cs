using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BankService.Api.HealthChecks;
using BankService.Api.Helpers;
using BankService.Api.Middleware;
using BankService.Application.Interfaces;
using BankService.Infrastructure.Data;
using BankService.Infrastructure.Identity;
using BankService.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtOptions.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        builder.Logging.AddFilter("BankService", LogLevel.Information);
        Console.WriteLine("[BankService] Jwt:Key was not configured. An ephemeral development key was generated; set Jwt__Key to keep tokens valid across restarts.");
    }
    else
    {
        throw new InvalidOperationException(
            "Jwt:Key is not configured. Set the Jwt__Key environment variable (or user secrets) before starting the API outside of Development.");
    }
}

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)) jwtOptions.Issuer = "BankServicePortal";
if (string.IsNullOrWhiteSpace(jwtOptions.Audience)) jwtOptions.Audience = "BankServicePortal";
if (jwtOptions.ExpiryMinutes <= 0) jwtOptions.ExpiryMinutes = 60;

builder.Services.AddControllers(options => options.Filters.Add<ErrorTraceIdFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The value provided is not valid."
                            : error.ErrorMessage)
                        .ToArray());

            var payload = new ErrorPayload
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "One or more validation errors occurred.",
                Errors = errors,
                TraceId = context.HttpContext.TraceIdentifier
            };

            return new BadRequestObjectResult(payload);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bank Service Portal API",
        Version = "v1",
        Description = "Internal banking service-request management system API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. Set the ConnectionStrings__DefaultConnection environment variable.");
    }

    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
});
builder.Services.AddScoped<IDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

builder.Services.AddAppIdentity();

builder.Services.Configure<JwtOptions>(options =>
{
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.Key = jwtOptions.Key;
    options.ExpiryMinutes = jwtOptions.ExpiryMinutes;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorPayload
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Authentication is required to access this resource.",
                TraceId = context.HttpContext.TraceIdentifier
            });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorPayload
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "You do not have permission to perform this action.",
                TraceId = context.HttpContext.TraceIdentifier
            });
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("SupportOrAbove", policy => policy.RequireRole("Admin", "Manager", "Support"));
});

// Partitioning by client is what makes the login limit meaningful: a shared
// counter would let a single busy office exhaust the budget for every other
// user, and an unpartitioned API limit would penalise small tenants on shared
// infrastructure. Forwarded headers are required for the client IP to be correct
// when the app sits behind a reverse proxy.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Applies to every endpoint that does not declare its own policy. A named
    // policy alone would be inert, since nothing opts into it; login overrides
    // this with its tighter budget.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IRequestNumberService, RequestNumberService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:8080", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Forwarded headers must be resolved before the rate limiter so partitions are
// keyed on the real client IP rather than the reverse proxy's.
//
// ForwardLimit = 1 means only the right-most forwarded entry is trusted. The
// bundled nginx config appends the real peer address to X-Forwarded-For, so a
// client-supplied value ends up on the left and is ignored. That keeps the
// limiter correct behind a trusted proxy without letting a caller spoof its way
// past the limit.
//
// KnownProxies/KnownNetworks are cleared because in the Compose topology the
// proxy's address is a container IP that changes between runs. Set
// ForwardedHeaders:TrustKnownProxies=false (the default) for the permissive
// behaviour, or list KnownProxies explicitly to tighten it.
var trustKnownProxies = app.Configuration.GetValue("ForwardedHeaders:TrustKnownProxies", false);
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
if (!trustKnownProxies)
{
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}
else
{
    foreach (var proxy in app.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(proxy, out var address))
        {
            forwardedHeadersOptions.KnownProxies.Add(address);
        }
        else
        {
            app.Logger.LogWarning("Ignoring invalid ForwardedHeaders:KnownProxies entry {Proxy}.", proxy);
        }
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready");

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (context.Database.IsRelational())
    {
        var attempts = 0;
        while (true)
        {
            attempts++;
            try
            {
                logger.LogInformation("Applying database migrations (attempt {Attempt})...", attempts);
                await context.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempts < 15)
            {
                logger.LogWarning(ex, "Database not ready yet, retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
    else
    {
        await context.Database.EnsureCreatedAsync();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<BankService.Domain.Entities.ApplicationUser>>();
    await DataSeeder.SeedAsync(context, roleManager, userManager, logger);
}

app.Run();

public partial class Program { }
