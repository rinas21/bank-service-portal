namespace BankService.Domain.Entities;

public class RequestAssignment
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;
    public string AssigneeId { get; set; } = string.Empty;
    public ApplicationUser Assignee { get; set; } = null!;
    public string AssignedById { get; set; } = string.Empty;
    public ApplicationUser AssignedBy { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
}
