using BankService.Domain.Enums;

namespace BankService.Domain.Entities;

public class Approval
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;
    public string RequestedById { get; set; } = string.Empty;
    public ApplicationUser RequestedBy { get; set; } = null!;
    public string? ApproverId { get; set; }
    public ApplicationUser? Approver { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Reason { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
