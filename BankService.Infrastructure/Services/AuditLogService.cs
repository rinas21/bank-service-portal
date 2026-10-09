using BankService.Application.Common;
using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BankService.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IDbContext _dbContext;

    public AuditLogService(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AuditLogDto>> GetListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Pagination.Normalize(query.Page, query.PageSize);
        var q = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(l => (l.UserName != null && l.UserName.ToLower().Contains(s))
                || (l.Details != null && l.Details.ToLower().Contains(s))
                || (l.EntityId != null && l.EntityId.ToLower().Contains(s))
                || (l.EntityType.ToLower().Contains(s)));
        }

        if (query.Action.HasValue) q = q.Where(l => l.Action == query.Action.Value);
        if (!string.IsNullOrWhiteSpace(query.UserId)) q = q.Where(l => l.UserId == query.UserId);
        if (query.From.HasValue) q = q.Where(l => l.Timestamp >= query.From.Value);
        if (query.To.HasValue) q = q.Where(l => l.Timestamp <= query.To.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(l => new AuditLogDto(l.Id, l.UserId, l.UserName, l.Action.ToString(),
            l.EntityType, l.EntityId, l.Details, l.IpAddress, l.Timestamp)).ToList();

        return new PagedResult<AuditLogDto>(dtos, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task LogAsync(AuditLogDto entry, CancellationToken ct = default)
    {
        _dbContext.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            UserId = entry.UserId,
            UserName = entry.UserName,
            Action = Enum.Parse<AuditAction>(entry.Action),
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Details = entry.Details,
            IpAddress = entry.IpAddress
        });
        await _dbContext.SaveChangesAsync(ct);
    }
}
