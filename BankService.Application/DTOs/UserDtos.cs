using System.ComponentModel.DataAnnotations;

namespace BankService.Application.DTOs;

public record CreateUserRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [StringLength(20)] string? EmployeeNumber,
    int? BranchId,
    [Required] IReadOnlyList<string> Roles);

public record UpdateUserRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(20)] string? EmployeeNumber,
    int? BranchId,
    [Required] IReadOnlyList<string> Roles);

public record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? EmployeeNumber,
    int? BranchId,
    string? BranchName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);

/// <summary>
/// A reduced user record for pickers that only need to identify someone, such
/// as the request assignment list. Exposes no email, branch or account state,
/// so it can be served to roles that cannot read the full user administration
/// list.
/// </summary>
public record AssignableUserDto(
    string Id,
    string FullName,
    IReadOnlyList<string> Roles);
