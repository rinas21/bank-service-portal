using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankService.Application.DTOs;
using BankService.Domain.Entities;
using BankService.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace BankService.Tests.Integration;

internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

[Collection("IntegrationTests")]
public class ApiIntegrationTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests()
    {
        var dbName = $"IntegrationTestDb_{Guid.NewGuid():N}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    private void SetAuthToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string firstName, string lastName, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, "Password@123");
        result.Succeeded.Should().BeTrue();
        foreach (var role in roles)
        {
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        await CreateUserAsync("test@bankportal.com", "Test", "User");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test@bankportal.com",
            password = "Password@123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.Email.Should().Be("test@bankportal.com");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsError()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nonexistent@test.com",
            password = "wrongpassword"
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"response body: {body}");
    }

    [Fact]
    public async Task GetRequests_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/servicerequests");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRequests_WithAuth_ReturnsRequests()
    {
        await CreateUserAsync("employee@test.com", "Employee", "User", "Employee");
        var token = await GetAuthTokenAsync("employee@test.com", "Password@123");
        SetAuthToken(token);

        var response = await _client.GetAsync("/api/servicerequests");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ServiceRequestSummaryDto>>(TestJson.Options);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRequest_WithValidData_CreatesRequest()
    {
        await CreateUserAsync("creator@test.com", "Creator", "User", "Employee");
        var token = await GetAuthTokenAsync("creator@test.com", "Password@123");
        SetAuthToken(token);

        var response = await _client.PostAsJsonAsync("/api/servicerequests", new
        {
            title = "Integration Test Request",
            description = "This is a test request",
            category = "Technical Support",
            priority = 1
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"response body: {body}");
        var result = await response.Content.ReadFromJsonAsync<ServiceRequestDetailDto>(TestJson.Options);
        result.Should().NotBeNull();
        result!.Title.Should().Be("Integration Test Request");
    }

    [Fact]
    public async Task GetDashboard_WithAuth_ReturnsStats()
    {
        await CreateUserAsync("dashboard@test.com", "Dashboard", "User", "Employee");
        var token = await GetAuthTokenAsync("dashboard@test.com", "Password@123");
        SetAuthToken(token);

        var response = await _client.GetAsync("/api/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DashboardStatsDto>(TestJson.Options);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminEndpoint_WithNonAdminRole_ReturnsForbidden()
    {
        await CreateUserAsync("nonadmin@test.com", "NonAdmin", "User", "Employee");
        var token = await GetAuthTokenAsync("nonadmin@test.com", "Password@123");
        SetAuthToken(token);

        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Support")]
    public async Task AssignableUsers_AvailableToEveryRoleThatCanAssign(string role)
    {
        var email = $"{role.ToLowerInvariant()}@assign.test";
        await CreateUserAsync(email, role, "Assignee", role);
        var token = await GetAuthTokenAsync(email, "Password@123");
        SetAuthToken(token);

        var response = await _client.GetAsync("/api/users/assignable");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The seeded data includes support agents, so a non-empty list is the
        // baseline; the caller's own role must not be filtered out either.
        var users = await response.Content.ReadFromJsonAsync<List<AssignableUserDto>>(TestJson.Options);
        users.Should().NotBeNull();
        users.Should().NotBeEmpty();
        users.Should().OnlyContain(u => u.Roles.Any(r => r == "Admin" || r == "Manager" || r == "Support"));
    }

    [Fact]
    public async Task AssignableUsers_ExcludesEmployeesAndInactiveAccounts()
    {
        var employee = await CreateUserAsync("picker-employee@test.com", "Picker", "Employee", "Employee");
        var support = await CreateUserAsync("picker-support@test.com", "Picker", "Support", "Support");

        var activeSupport = await CreateUserAsync("picker-active@test.com", "Picker", "Active", "Support");

        // Reload inside a fresh scope: the instance returned by the helper is
        // still tracked by the scope that created it.
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var id in new[] { employee.Id, support.Id })
            {
                var reloaded = await userManager.FindByIdAsync(id);
                reloaded.Should().NotBeNull();
                reloaded!.IsActive = false;
                (await userManager.UpdateAsync(reloaded)).Succeeded.Should().BeTrue();
            }
        }

        await CreateUserAsync("picker-admin@test.com", "Picker", "Admin", "Admin");
        SetAuthToken(await GetAuthTokenAsync("picker-admin@test.com", "Password@123"));

        var users = await _client.GetFromJsonAsync<List<AssignableUserDto>>("/api/users/assignable", TestJson.Options);

        users.Should().NotBeNull();
        // An employee has no assignment capability, and a deactivated support
        // agent cannot take work, so neither should be offered.
        users.Should().NotContain(u => u.Id == employee.Id);
        users.Should().NotContain(u => u.Id == support.Id);
        users.Should().Contain(u => u.Id == activeSupport.Id);
    }

    [Fact]
    public async Task AssignableUsers_WithEmployeeRole_ReturnsForbidden()
    {
        await CreateUserAsync("picker-bare@test.com", "Picker", "Bare", "Employee");
        SetAuthToken(await GetAuthTokenAsync("picker-bare@test.com", "Password@123"));

        var response = await _client.GetAsync("/api/users/assignable");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
