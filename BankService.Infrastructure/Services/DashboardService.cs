using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BankService.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IDbContext _dbContext;

    public DashboardService(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(string currentUserId, string role, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-7);

        var requestsQuery = _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .AsNoTracking()
            .AsQueryable();

        if (role == "Employee")
        {
            requestsQuery = requestsQuery.Where(r => r.RequesterId == currentUserId);
        }
        else if (role == "Support")
        {
            requestsQuery = requestsQuery.Where(r => r.AssignedToId == currentUserId);
        }

        var requests = await requestsQuery.ToListAsync(ct);

        var totalRequests = requests.Count;
        var openRequests = requests.Count(r => r.Status == RequestStatus.Open);
        var inProgressRequests = requests.Count(r => r.Status == RequestStatus.InProgress);
        var resolvedRequests = requests.Count(r => r.Status == RequestStatus.Resolved);
        var closedRequests = requests.Count(r => r.Status == RequestStatus.Closed);
        var criticalRequests = requests.Count(r => r.Priority == RequestPriority.Critical && r.Status != RequestStatus.Closed && r.Status != RequestStatus.Resolved);
        var overdueRequests = requests.Count(r => r.DueDate < now && r.Status != RequestStatus.Closed && r.Status != RequestStatus.Resolved);
        var requestsThisWeek = requests.Count(r => r.CreatedAt >= weekStart);

        var pendingApprovals = await _dbContext.Approvals
            .AsNoTracking()
            .CountAsync(a => a.Status == ApprovalStatus.Pending, ct);

        var byStatus = requests
            .GroupBy(r => r.Status)
            .Select(g => new StatusCountDto(g.Key.ToString(), g.Count()))
            .ToList();

        var byPriority = requests
            .GroupBy(r => r.Priority)
            .Select(g => new PriorityCountDto(g.Key.ToString(), g.Count()))
            .ToList();

        var byCategory = requests
            .GroupBy(r => r.Category)
            .Select(g => new CategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(8)
            .ToList();

        var recentRequests = requests
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new RecentRequestDto(r.Id, r.RequestNumber, r.Title, r.Status, r.Priority, r.Requester.FullName, r.CreatedAt))
            .ToList();

        var recentActivity = await _dbContext.StatusHistory
            .Include(h => h.ServiceRequest)
            .Include(h => h.ChangedBy)
            .AsNoTracking()
            .OrderByDescending(h => h.ChangedAt)
            .Take(10)
            .Select(h => new RequestActivityDto(h.ServiceRequestId, h.ServiceRequest.RequestNumber, h.ServiceRequest.Title,
                $"Status changed to {h.ToStatus}", h.ChangedBy.FullName, h.ChangedAt))
            .ToListAsync(ct);

        return new DashboardStatsDto(
            totalRequests, openRequests, inProgressRequests, resolvedRequests, closedRequests,
            criticalRequests, pendingApprovals, overdueRequests, requestsThisWeek,
            byStatus, byPriority, byCategory, recentRequests, recentActivity);
    }
}
