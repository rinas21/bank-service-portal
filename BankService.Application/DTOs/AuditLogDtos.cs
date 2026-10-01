using BankService.Domain.Enums;

namespace BankService.Application.DTOs;

public record AuditLogDto(
    int Id,
    string? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? Details,
    string? IpAddress,
    DateTime Timestamp);

public record AuditLogQuery(
    string? Search,
    AuditAction? Action,
    string? UserId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);
