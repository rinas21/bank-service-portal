using BankService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankService.Application.Interfaces;

public interface IDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<Branch> Branches { get; }
    DbSet<ServiceRequest> ServiceRequests { get; }
    DbSet<Comment> Comments { get; }
    DbSet<RequestAssignment> Assignments { get; }
    DbSet<StatusHistory> StatusHistory { get; }
    DbSet<Approval> Approvals { get; }
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
