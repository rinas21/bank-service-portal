using BankService.Application.Interfaces;
using BankService.Infrastructure.Data;
using BankService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICurrentUserService, StubCurrentUserService>();
        services.AddScoped<IRequestNumberService, RequestNumberService>();
        services.AddScoped<ICsvImportService, CsvImportService>();
    })
    .Build();

var configuration = host.Services.GetRequiredService<IConfiguration>();
var csvPath = args.Length > 0 ? args[0] : Path.Combine("database", "csv", "sample-legacy-requests.csv");
var connectionString = configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:DefaultConnection is required (provide via appsettings.json or environment).");
    return 1;
}

if (!File.Exists(csvPath))
{
    Console.Error.WriteLine($"CSV file not found: {csvPath}");
    return 2;
}

var userId = configuration["Importer:UserId"] ?? "csv-importer";
var userName = configuration["Importer:UserName"] ?? "CSV Importer";

using (var scope = host.Services.CreateScope())
{
    var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
    (currentUser as StubCurrentUserService)?.SetUser(userId, userName, new[] { "Admin" });

    var importService = scope.ServiceProvider.GetRequiredService<ICsvImportService>();
    await using var stream = File.OpenRead(csvPath);
    var result = await importService.ImportAsync(stream, userId);
    Console.WriteLine("=== CSV IMPORT RESULT ===");
    Console.WriteLine($"Total: {result.TotalRecords} | Imported: {result.Imported} | Duplicates: {result.Duplicates} | Rejected: {result.Rejected}");
    foreach (var r in result.RowResults)
    {
        Console.WriteLine($"[{r.RowNumber}] {r.Status}: {r.LegacyReference ?? "-"} -> {r.Message}");
    }
}

return 0;

/// <summary>
/// Standalone importer has no HTTP context, so it supplies the identity that the
/// shared CsvImportService uses when writing its audit entry.
/// </summary>
public class StubCurrentUserService : ICurrentUserService
{
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? Email { get; private set; } = "csv-importer@bankportal.com";
    public bool IsAuthenticated => UserId is not null;
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public RequestActor Actor { get; private set; } = RequestActor.Manager;

    public string PrimaryRole =>
        new[] { "Admin", "Manager", "Support", "Employee" }.FirstOrDefault(IsInRole) ?? "Employee";

    public void SetUser(string userId, string userName, IReadOnlyList<string> roles)
    {
        UserId = userId;
        UserName = userName;
        Roles = roles;
    }

    public bool IsInRole(string role) => Roles.Contains(role);
}
