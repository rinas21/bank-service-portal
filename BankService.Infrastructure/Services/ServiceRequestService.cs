using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankService.Infrastructure.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly IDbContext _dbContext;
    private readonly IRequestNumberService _requestNumberService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ServiceRequestService> _logger;

    public ServiceRequestService(
        IDbContext dbContext,
        IRequestNumberService requestNumberService,
        ICurrentUserService currentUser,
        ILogger<ServiceRequestService> logger)
    {
        _dbContext = dbContext;
        _requestNumberService = requestNumberService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PagedResult<ServiceRequestSummaryDto>> GetListAsync(
        ServiceRequestListQuery query, string currentUserId, bool isAdminOrManager, CancellationToken ct = default)
    {
        var q = _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .AsNoTracking()
            .AsQueryable();

        if (!isAdminOrManager)
        {
            q = q.Where(r => r.RequesterId == currentUserId || r.AssignedToId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            q = q.Where(r => r.Title.ToLower().Contains(search)
                || r.RequestNumber.ToLower().Contains(search)
                || r.Description.ToLower().Contains(search)
                || r.Category.ToLower().Contains(search));
        }

        if (query.Status.HasValue) q = q.Where(r => r.Status == query.Status.Value);
        if (query.Priority.HasValue) q = q.Where(r => r.Priority == query.Priority.Value);
        if (!string.IsNullOrWhiteSpace(query.Category)) q = q.Where(r => r.Category == query.Category);
        if (query.BranchId.HasValue) q = q.Where(r => r.BranchId == query.BranchId.Value);
        if (!string.IsNullOrWhiteSpace(query.AssignedToId)) q = q.Where(r => r.AssignedToId == query.AssignedToId);
        if (!string.IsNullOrWhiteSpace(query.RequesterId)) q = q.Where(r => r.RequesterId == query.RequesterId);
        if (query.RequiresApproval.HasValue) q = q.Where(r => r.RequiresApproval == query.RequiresApproval.Value);

        q = query.SortBy?.ToLower() switch
        {
            "priority" => query.Descending ? q.OrderByDescending(r => r.Priority) : q.OrderBy(r => r.Priority),
            "status" => query.Descending ? q.OrderByDescending(r => r.Status) : q.OrderBy(r => r.Status),
            "title" => query.Descending ? q.OrderByDescending(r => r.Title) : q.OrderBy(r => r.Title),
            "duedate" => query.Descending ? q.OrderByDescending(r => r.DueDate) : q.OrderBy(r => r.DueDate),
            "createdat" => query.Descending ? q.OrderByDescending(r => r.CreatedAt) : q.OrderBy(r => r.CreatedAt),
            _ => q.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(r => new ServiceRequestSummaryDto(
            r.Id,
            r.RequestNumber,
            r.Title,
            r.Category,
            r.Status,
            r.Priority,
            r.Requester.FullName,
            r.AssignedTo?.FullName,
            r.Branch?.Name,
            r.RequiresApproval,
            r.IsMigrated,
            r.CreatedAt,
            r.DueDate,
            r.ResolvedAt,
            r.Comments.Count)).ToList();

        return new PagedResult<ServiceRequestSummaryDto>(
            dtos, totalCount, query.Page, query.PageSize,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }

    public async Task<ServiceRequestDetailDto?> GetByIdAsync(int id, string currentUserId, bool isAdminOrManager, CancellationToken ct = default)
    {
        var r = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .Include(r => r.Comments).ThenInclude(c => c.Author)
            .Include(r => r.StatusHistory).ThenInclude(h => h.ChangedBy)
            .Include(r => r.Assignments).ThenInclude(a => a.Assignee)
            .Include(r => r.Assignments).ThenInclude(a => a.AssignedBy)
            .Include(r => r.Approvals).ThenInclude(a => a.RequestedBy)
            .Include(r => r.Approvals).ThenInclude(a => a.Approver)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (r is null) return null;
        if (!isAdminOrManager && r.RequesterId != currentUserId && r.AssignedToId != currentUserId)
        {
            throw new UnauthorizedAccessException("You do not have access to this request.");
        }

        return MapToDetail(r, isAdminOrManager);
    }

    public async Task<ServiceRequestDetailDto> CreateAsync(CreateServiceRequestRequest request, string currentUserId, CancellationToken ct = default)
    {
        var requestNumber = await _requestNumberService.GenerateRequestNumberAsync(ct);

        var entity = new ServiceRequest
        {
            RequestNumber = requestNumber,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            BranchId = request.BranchId,
            DueDate = request.DueDate,
            RequesterId = currentUserId,
            Status = RequestStatus.Open
        };

        _dbContext.ServiceRequests.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _dbContext.StatusHistory.Add(new StatusHistory
        {
            ServiceRequestId = entity.Id,
            FromStatus = RequestStatus.Open,
            ToStatus = RequestStatus.Open,
            ChangedById = currentUserId,
            Reason = "Request created"
        });
        await _dbContext.SaveChangesAsync(ct);

        await LogAuditAsync(AuditAction.RequestCreated, "ServiceRequest", entity.Id.ToString(),
            $"Created request {entity.RequestNumber}", ct);

        return await GetByIdAsync(entity.Id, currentUserId, true, ct)
            ?? throw new InvalidOperationException("Failed to retrieve created request.");
    }

    public async Task<ServiceRequestDetailDto?> UpdateAsync(int id, UpdateServiceRequestRequest request, string currentUserId, bool isAdminOrManager, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        if (!isAdminOrManager && entity.RequesterId != currentUserId)
        {
            throw new UnauthorizedAccessException("Only the requester or an admin can edit this request.");
        }

        if (entity.Status is RequestStatus.Closed or RequestStatus.Resolved)
        {
            throw new InvalidOperationException("Resolved or closed requests cannot be edited.");
        }

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.Priority = request.Priority;
        entity.BranchId = request.BranchId;
        entity.DueDate = request.DueDate;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.RequestUpdated, "ServiceRequest", id.ToString(), $"Updated request {entity.RequestNumber}", ct);

        return await GetByIdAsync(id, currentUserId, isAdminOrManager, ct);
    }

    public async Task<ServiceRequestDetailDto?> UpdateStatusAsync(int id, UpdateStatusRequest request, string currentUserId, bool isAdminOrManager, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        if (!isAdminOrManager && entity.RequesterId != currentUserId && entity.AssignedToId != currentUserId)
        {
            throw new UnauthorizedAccessException("You do not have access to this request.");
        }

        if (entity.Status == request.Status)
        {
            throw new InvalidOperationException("The request is already in this status.");
        }

        var fromStatus = entity.Status;
        entity.Status = request.Status;
        entity.UpdatedAt = DateTime.UtcNow;

        if (request.Status == RequestStatus.Resolved) entity.ResolvedAt = DateTime.UtcNow;
        if (request.Status == RequestStatus.Closed) entity.ClosedAt = DateTime.UtcNow;

        _dbContext.StatusHistory.Add(new StatusHistory
        {
            ServiceRequestId = id,
            FromStatus = fromStatus,
            ToStatus = request.Status,
            ChangedById = currentUserId,
            Reason = request.Reason
        });

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.StatusChanged, "ServiceRequest", id.ToString(),
            $"Status changed from {fromStatus} to {request.Status} for {entity.RequestNumber}", ct);

        return await GetByIdAsync(id, currentUserId, isAdminOrManager, ct);
    }

    public async Task<ServiceRequestDetailDto?> AssignAsync(int id, AssignRequest request, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        var assignee = await _dbContext.Users.FindAsync(new object[] { request.AssigneeId }, ct)
            ?? throw new KeyNotFoundException("Assignee not found.");

        if (!assignee.IsActive)
        {
            throw new InvalidOperationException("Cannot assign to an inactive user.");
        }

        entity.AssignedToId = request.AssigneeId;
        entity.UpdatedAt = DateTime.UtcNow;

        if (entity.Status == RequestStatus.Open)
        {
            var fromStatus = entity.Status;
            entity.Status = RequestStatus.InProgress;
            _dbContext.StatusHistory.Add(new StatusHistory
            {
                ServiceRequestId = id,
                FromStatus = fromStatus,
                ToStatus = RequestStatus.InProgress,
                ChangedById = currentUserId,
                Reason = $"Assigned to {assignee.FullName}"
            });
        }

        _dbContext.Assignments.Add(new RequestAssignment
        {
            ServiceRequestId = id,
            AssigneeId = request.AssigneeId,
            AssignedById = currentUserId,
            Note = request.Note
        });

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.RequestAssigned, "ServiceRequest", id.ToString(),
            $"Assigned {entity.RequestNumber} to {assignee.FullName}", ct);

        return await GetByIdAsync(id, currentUserId, true, ct);
    }

    public async Task<ServiceRequestDetailDto?> AddCommentAsync(int id, AddCommentRequest request, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        _dbContext.Comments.Add(new Comment
        {
            ServiceRequestId = id,
            AuthorId = currentUserId,
            Body = request.Body,
            IsInternal = request.IsInternal
        });

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.CommentAdded, "ServiceRequest", id.ToString(),
            $"Comment added to {entity.RequestNumber}", ct);

        return await GetByIdAsync(id, currentUserId, true, ct);
    }

    public async Task<ServiceRequestDetailDto?> RequestApprovalAsync(int id, string reason, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        if (entity.RequesterId != currentUserId)
        {
            throw new UnauthorizedAccessException("Only the requester can request approval.");
        }

        if (entity.Approvals.Any(a => a.Status == ApprovalStatus.Pending))
        {
            throw new InvalidOperationException("There is already a pending approval for this request.");
        }

        entity.RequiresApproval = true;
        entity.UpdatedAt = DateTime.UtcNow;

        _dbContext.Approvals.Add(new Approval
        {
            ServiceRequestId = id,
            RequestedById = currentUserId,
            Reason = reason
        });

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(AuditAction.ApprovalRequested, "ServiceRequest", id.ToString(),
            $"Approval requested for {entity.RequestNumber}", ct);

        return await GetByIdAsync(id, currentUserId, true, ct);
    }

    public async Task<ServiceRequestDetailDto?> DecideApprovalAsync(int id, ApprovalDecisionRequest request, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _dbContext.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.AssignedTo)
            .Include(r => r.Branch)
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Request not found.");

        var approval = entity.Approvals
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefault(a => a.Status == ApprovalStatus.Pending)
            ?? throw new InvalidOperationException("There is no pending approval for this request.");

        if (approval.RequestedById == currentUserId)
        {
            throw new UnauthorizedAccessException("You cannot approve your own request.");
        }

        approval.Status = request.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ApproverId = currentUserId;
        approval.DecisionNote = request.Note;
        approval.DecidedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await LogAuditAsync(request.Approve ? AuditAction.ApprovalGranted : AuditAction.ApprovalRejected,
            "ServiceRequest", id.ToString(),
            $"Approval {(request.Approve ? "granted" : "rejected")} for {entity.RequestNumber}", ct);

        return await GetByIdAsync(id, currentUserId, true, ct);
    }

    private ServiceRequestDetailDto MapToDetail(ServiceRequest r, bool isAdminOrManager)
    {
        var comments = r.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Where(c => isAdminOrManager || !c.IsInternal)
            .Select(c => new CommentDto(c.Id, c.AuthorId, c.Author.FullName, c.Body, c.IsInternal, c.CreatedAt))
            .ToList();

        var history = r.StatusHistory
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new StatusHistoryDto(h.Id, h.FromStatus.ToString(), h.ToStatus.ToString(),
                h.ChangedBy.FullName, h.Reason, h.ChangedAt))
            .ToList();

        var assignments = r.Assignments
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new AssignmentDto(a.Id, a.AssigneeId, a.Assignee.FullName, a.AssignedBy.FullName,
                a.Note, a.AssignedAt, a.UnassignedAt))
            .ToList();

        var approvals = r.Approvals
            .OrderByDescending(a => a.RequestedAt)
            .Select(a => new ApprovalDto(a.Id, a.RequestedBy.FullName, a.Approver?.FullName,
                a.Status.ToString(), a.Reason, a.DecisionNote, a.RequestedAt, a.DecidedAt))
            .ToList();

        return new ServiceRequestDetailDto(
            r.Id, r.RequestNumber, r.Title, r.Description, r.Category, r.Status, r.Priority,
            r.RequesterId, r.Requester.FullName, r.BranchId, r.Branch?.Name,
            r.AssignedToId, r.AssignedTo?.FullName, r.RequiresApproval, r.IsMigrated, r.LegacyReference,
            r.CreatedAt, r.UpdatedAt, r.ResolvedAt, r.ClosedAt, r.DueDate,
            comments, history, assignments, approvals);
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
