using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BankService.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly IDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public BranchService(IDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<BranchDto>> GetListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbContext.Branches
            .Include(b => b.Users)
            .Include(b => b.ServiceRequests)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(s)
                || b.Code.ToLower().Contains(s)
                || b.City.ToLower().Contains(s));
        }

        if (isActive.HasValue) query = query.Where(b => b.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);
        var branches = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = branches.Select(b => new BranchDto(
            b.Id, b.Code, b.Name, b.City, b.Address, b.Phone, b.IsActive,
            b.Users.Count,
            b.ServiceRequests.Count(r => r.Status == Domain.Enums.RequestStatus.Open || r.Status == Domain.Enums.RequestStatus.InProgress),
            b.CreatedAt)).ToList();

        return new PagedResult<BranchDto>(dtos, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<BranchDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var b = await _dbContext.Branches
            .Include(b => b.Users)
            .Include(b => b.ServiceRequests)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (b is null) return null;

        return new BranchDto(b.Id, b.Code, b.Name, b.City, b.Address, b.Phone, b.IsActive,
            b.Users.Count,
            b.ServiceRequests.Count(r => r.Status == Domain.Enums.RequestStatus.Open || r.Status == Domain.Enums.RequestStatus.InProgress),
            b.CreatedAt);
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        if (await _dbContext.Branches.AnyAsync(b => b.Code == request.Code, ct))
        {
            throw new InvalidOperationException("A branch with this code already exists.");
        }

        var branch = new Branch
        {
            Code = request.Code,
            Name = request.Name,
            City = request.City,
            Address = request.Address,
            Phone = request.Phone
        };

        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync(ct);

        await LogAuditAsync(AuditAction.BranchCreated, "Branch", branch.Id.ToString(), $"Created branch {branch.Code} - {branch.Name}", ct);

        return new BranchDto(branch.Id, branch.Code, branch.Name, branch.City, branch.Address, branch.Phone, branch.IsActive, 0, 0, branch.CreatedAt);
    }

    public async Task<BranchDto?> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken ct = default)
    {
        var branch = await _dbContext.Branches.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Branch not found.");

        if (branch.Code != request.Code && await _dbContext.Branches.AnyAsync(b => b.Code == request.Code && b.Id != id, ct))
        {
            throw new InvalidOperationException("A branch with this code already exists.");
        }

        branch.Code = request.Code;
        branch.Name = request.Name;
        branch.City = request.City;
        branch.Address = request.Address;
        branch.Phone = request.Phone;
        branch.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.BranchUpdated, "Branch", branch.Id.ToString(), $"Updated branch {branch.Code} - {branch.Name}", ct);

        return await GetByIdAsync(id, ct);
    }

    private async Task LogAuditAsync(AuditAction action, string entityType, string? entityId, string? details, CancellationToken ct)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await _dbContext.SaveChangesAsync(ct);
    }
}
