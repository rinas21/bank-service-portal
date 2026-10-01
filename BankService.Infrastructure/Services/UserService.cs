using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankService.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        IDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> GetListAsync(string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _userManager.Users
            .Include(u => u.Branch)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.FirstName.ToLower().Contains(s)
                || u.LastName.ToLower().Contains(s)
                || u.Email!.ToLower().Contains(s)
                || (u.EmployeeNumber != null && u.EmployeeNumber.ToLower().Contains(s)));
        }

        if (isActive.HasValue) query = query.Where(u => u.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = new List<UserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role)) continue;
            dtos.Add(MapToDto(u, roles));
        }

        return new PagedResult<UserDto>(dtos, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await _userManager.Users
            .Include(u => u.Branch)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;
        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmployeeNumber = request.EmployeeNumber,
            BranchId = request.BranchId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        foreach (var role in request.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        await LogAuditAsync(AuditAction.UserCreated, "User", user.Id, $"Created user {user.Email}", ct);

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<UserDto?> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        var emailChanged = !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing is not null && existing.Id != id)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.UserName = request.Email;
        user.EmployeeNumber = request.EmployeeNumber;
        user.BranchId = request.BranchId;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"User update failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded) throw new InvalidOperationException("Failed to remove roles.");
        }
        foreach (var role in rolesToAdd)
        {
            var addResult = await _userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded) throw new InvalidOperationException($"Failed to assign role '{role}'.");
        }

        await LogAuditAsync(AuditAction.UserUpdated, "User", user.Id, $"Updated user {user.Email}", ct);

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<bool> DeactivateAsync(string id, CancellationToken ct = default)
    {
        if (id == _currentUser.UserId)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return false;

        await LogAuditAsync(AuditAction.UserDeactivated, "User", user.Id, $"Deactivated user {user.Email}", ct);
        return true;
    }

    public async Task<bool> ReactivateAsync(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return false;

        await LogAuditAsync(AuditAction.UserUpdated, "User", user.Id, $"Reactivated user {user.Email}", ct);
        return true;
    }

    private static UserDto MapToDto(ApplicationUser u, IEnumerable<string> roles) => new(
        u.Id, u.FirstName, u.LastName, u.FullName, u.Email!, u.EmployeeNumber,
        u.BranchId, u.Branch?.Name, u.IsActive, u.CreatedAt, u.LastLoginAt, roles.ToList());

    private async Task LogAuditAsync(AuditAction action, string entityType, string? entityId, string? details, CancellationToken ct)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await _dbContext.SaveChangesAsync(ct);
    }
}
