using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> GetListAsync(AuditLogQuery query, CancellationToken ct = default);
    Task LogAsync(AuditLogDto entry, CancellationToken ct = default);
}
