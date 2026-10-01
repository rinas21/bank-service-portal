using BankService.Domain.Enums;

namespace BankService.Domain.Entities;

public class StatusHistory
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;
    public RequestStatus FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }
    public string ChangedById { get; set; } = string.Empty;
    public ApplicationUser ChangedBy { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
