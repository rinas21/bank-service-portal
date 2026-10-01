using BankService.Domain.Enums;

namespace BankService.Domain.Entities;

public class ServiceRequest
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser Requester { get; set; } = null!;

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public bool RequiresApproval { get; set; }
    public bool IsMigrated { get; set; }
    public string? LegacyReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? DueDate { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<RequestAssignment> Assignments { get; set; } = new List<RequestAssignment>();
    public ICollection<StatusHistory> StatusHistory { get; set; } = new List<StatusHistory>();
    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
}
