using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
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
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
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

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
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

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ServiceRequestSummaryDto>>();
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

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ServiceRequestDetailDto>();
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

        var result = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();
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
}
