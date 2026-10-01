using System.ComponentModel.DataAnnotations;

namespace BankService.Application.DTOs;

public record CreateBranchRequest(
    [property: Required, StringLength(20)] string Code,
    [property: Required, StringLength(150)] string Name,
    [property: Required, StringLength(100)] string City,
    [property: StringLength(300)] string? Address,
    [property: StringLength(30)] string? Phone);

public record UpdateBranchRequest(
    [property: Required, StringLength(20)] string Code,
    [property: Required, StringLength(150)] string Name,
    [property: Required, StringLength(100)] string City,
    [property: StringLength(300)] string? Address,
    [property: StringLength(30)] string? Phone,
    bool IsActive);

public record BranchDto(
    int Id,
    string Code,
    string Name,
    string City,
    string? Address,
    string? Phone,
    bool IsActive,
    int UserCount,
    int OpenRequestCount,
    DateTime CreatedAt);
