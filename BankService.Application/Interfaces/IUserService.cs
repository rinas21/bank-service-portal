using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetListAsync(string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Active users who can be assigned work, as a reduced record. Available to
    /// the roles allowed to assign requests, not just admins.
    /// </summary>
    Task<IReadOnlyList<AssignableUserDto>> GetAssignableUsersAsync(CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> DeactivateAsync(string id, CancellationToken ct = default);
    Task<bool> ReactivateAsync(string id, CancellationToken ct = default);
}
