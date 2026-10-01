using System.Security.Claims;
using BankService.Application.Interfaces;
using BankService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace BankService.Tests.Unit;

public class CurrentUserServiceTests
{
    private const string UserId = "user-1";

    [Theory]
    // A single role resolves to itself.
    [InlineData(new[] { "Employee" }, "Employee")]
    [InlineData(new[] { "Support" }, "Support")]
    [InlineData(new[] { "Manager" }, "Manager")]
    [InlineData(new[] { "Admin" }, "Admin")]
    // Multi-role users resolve to their most privileged role regardless of the
    // order the claims happen to appear in.
    [InlineData(new[] { "Employee", "Manager" }, "Manager")]
    [InlineData(new[] { "Manager", "Employee" }, "Manager")]
    [InlineData(new[] { "Employee", "Support" }, "Support")]
    [InlineData(new[] { "Support", "Manager" }, "Manager")]
    [InlineData(new[] { "Manager", "Admin" }, "Admin")]
    [InlineData(new[] { "Employee", "Manager", "Admin" }, "Admin")]
    public void PrimaryRole_returns_most_privileged_role(string[] roles, string expected)
    {
        var service = BuildService(roles);

        Assert.Equal(expected, service.PrimaryRole);
    }

    [Fact]
    public void PrimaryRole_defaults_to_employee_when_no_role_is_assigned()
    {
        var service = BuildService([]);

        Assert.Equal("Employee", service.PrimaryRole);
    }

    [Theory]
    [InlineData(new[] { "Employee" }, RequestActor.Requester)]
    [InlineData(new[] { "Employee", "Support" }, RequestActor.SupportAgent)]
    [InlineData(new[] { "Employee", "Manager" }, RequestActor.Manager)]
    [InlineData(new[] { "Support", "Admin" }, RequestActor.Manager)]
    public void Actor_follows_the_same_precedence(string[] roles, RequestActor expected)
    {
        var service = BuildService(roles);

        Assert.Equal(expected, service.Actor);
    }

    [Fact]
    public void Anonymous_callers_have_no_actor_and_fall_back_to_employee_visibility()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor.Object);

        Assert.Equal(RequestActor.Anonymous, service.Actor);
        Assert.Equal("Employee", service.PrimaryRole);
        Assert.False(service.IsAuthenticated);
    }

    private static CurrentUserService BuildService(string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId),
            new(ClaimTypes.Name, "Test User"),
            new(ClaimTypes.Email, "test@example.com"),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(context);

        return new CurrentUserService(accessor.Object);
    }
}
