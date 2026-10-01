using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Enums;
using BankService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankService.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IDbContext dbContext,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginDto login, string? ipAddress, CancellationToken ct = default)
    {
        if (login == null) throw new ArgumentNullException(nameof(login));

        if (string.IsNullOrEmpty(login.Email) || string.IsNullOrEmpty(login.Password))
        {
            await LogAuditAsync(null, login.Email, AuditAction.LoginFailed, "User", null, "Failed login: missing credentials", ipAddress);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var user = await _userManager.FindByEmailAsync(login.Email);
        if (user is null || !user.IsActive)
        {
            await LogAuditAsync(null, login.Email, AuditAction.LoginFailed, "User", null, $"Failed login for {login.Email}", ipAddress);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, login.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await LogAuditAsync(user.Id, user.UserName, AuditAction.LoginFailed, "User", user.Id, $"Failed login for {user.Email}", ipAddress);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var token = _tokenService.GenerateJwtToken(user.Id, user.Email!, user.UserName!, roles);

        await LogAuditAsync(user.Id, user.UserName, AuditAction.Login, "User", user.Id, null, ipAddress);

        return new AuthResponse(
            token,
            _tokenService.GetTokenExpiry(),
            user.Id,
            user.Email!,
            user.FullName,
            user.EmployeeNumber ?? string.Empty,
            user.Branch?.Name,
            roles);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterDto register, string? ipAddress, CancellationToken ct = default)
    {
        if (register == null) throw new ArgumentNullException(nameof(register));

        if (string.IsNullOrEmpty(register.Email) || string.IsNullOrEmpty(register.Password))
        {
            throw new InvalidOperationException("Email and password are required.");
        }

        var existing = await _userManager.FindByEmailAsync(register.Email);
        if (existing is not null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = register.Email,
            Email = register.Email,
            FirstName = register.FirstName,
            LastName = register.LastName,
            EmployeeNumber = register.EmployeeNumber,
            BranchId = register.BranchId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, register.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await _userManager.AddToRoleAsync(user, "Employee");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateJwtToken(user.Id, user.Email!, user.UserName!, roles);

        await LogAuditAsync(user.Id, user.UserName, AuditAction.UserCreated, "User", user.Id, $"Self-registration for {user.Email}", ipAddress);

        return new AuthResponse(
            token,
            _tokenService.GetTokenExpiry(),
            user.Id,
            user.Email!,
            user.FullName,
            user.EmployeeNumber ?? string.Empty,
            user.Branch?.Name,
            roles);
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Password change failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    private async Task LogAuditAsync(string? userId, string? userName, AuditAction action, string entityType, string? entityId, string? details, string? ipAddress)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        });
        await _dbContext.SaveChangesAsync();
    }
}