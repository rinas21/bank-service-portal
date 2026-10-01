using System.ComponentModel.DataAnnotations;

namespace BankService.Application.DTOs;

public record CreateUserRequest(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(100, MinimumLength = 8)] string Password,
    [property: StringLength(20)] string? EmployeeNumber,
    int? BranchId,
    [property: Required] IReadOnlyList<string> Roles);

public record UpdateUserRequest(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: StringLength(20)] string? EmployeeNumber,
    int? BranchId,
    [property: Required] IReadOnlyList<string> Roles);

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
