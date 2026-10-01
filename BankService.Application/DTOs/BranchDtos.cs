using System.ComponentModel.DataAnnotations;

namespace BankService.Application.DTOs;

public record CreateBranchRequest(
    [Required, StringLength(20)] string Code,
    [Required, StringLength(150)] string Name,
    [Required, StringLength(100)] string City,
    [StringLength(300)] string? Address,
    [StringLength(30)] string? Phone);

public record UpdateBranchRequest(
    [Required, StringLength(20)] string Code,
    [Required, StringLength(150)] string Name,
    [Required, StringLength(100)] string City,
    [StringLength(300)] string? Address,
    [StringLength(30)] string? Phone,
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
