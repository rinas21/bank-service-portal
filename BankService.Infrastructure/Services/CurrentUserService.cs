using System.Security.Claims;
using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BankService.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        ?? new List<string>();

    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;

    // Ordered most privileged first. Kept in one place so role precedence is
    // decided consistently rather than by the order claims happen to appear.
    private static readonly string[] RolePrecedence = ["Admin", "Manager", "Support", "Employee"];

    public string PrimaryRole =>
        RolePrecedence.FirstOrDefault(IsInRole) ?? RolePrecedence[^1];

    public RequestActor Actor
    {
        get
        {
            if (!IsAuthenticated)
            {
                return RequestActor.Anonymous;
            }

            if (IsInRole("Admin") || IsInRole("Manager"))
            {
                return RequestActor.Manager;
            }

            return IsInRole("Support") ? RequestActor.SupportAgent : RequestActor.Requester;
        }
    }
}
