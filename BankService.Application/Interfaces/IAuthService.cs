using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginDto login, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterDto register, string? ipAddress, CancellationToken ct = default);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
}