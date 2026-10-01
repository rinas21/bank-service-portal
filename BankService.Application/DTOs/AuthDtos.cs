using System.ComponentModel.DataAnnotations;

namespace BankService.Application.DTOs;

public record LoginDto
{
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] [StringLength(100, MinimumLength = 6)] public string Password { get; set; } = string.Empty;
}

public record RegisterDto
{
    [Required] [StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required] [StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required] [EmailAddress] [StringLength(256)] public string Email { get; set; } = string.Empty;
    [StringLength(20)] public string? EmployeeNumber { get; set; }
    public int? BranchId { get; set; }
    [Required] [StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
}

public record ChangePasswordDto
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required] [StringLength(100, MinimumLength = 8)] public string NewPassword { get; set; } = string.Empty;
}

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    string FullName,
    string EmployeeNumber,
    string? BranchName,
    IList<string> Roles);