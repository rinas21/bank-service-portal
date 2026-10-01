using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankService.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<RequestAssignment> Assignments => Set<RequestAssignment>();
    public DbSet<StatusHistory> StatusHistory => Set<StatusHistory>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.Property(u => u.EmployeeNumber).HasMaxLength(20);
            e.HasIndex(u => u.EmployeeNumber).IsUnique(false);
            e.HasOne(u => u.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        builder.Entity<Branch>(e =>
        {
            e.ToTable("Branches");
            e.HasIndex(b => b.Code).IsUnique();
            e.Property(b => b.Code).HasMaxLength(20).IsRequired();
            e.Property(b => b.Name).HasMaxLength(150).IsRequired();
            e.Property(b => b.City).HasMaxLength(100).IsRequired();
            e.Property(b => b.Address).HasMaxLength(300);
            e.Property(b => b.Phone).HasMaxLength(30);
        });

        builder.Entity<ServiceRequest>(e =>
        {
            e.ToTable("ServiceRequests");
            e.HasIndex(r => r.RequestNumber).IsUnique();
            e.Property(r => r.RequestNumber).HasMaxLength(30).IsRequired();
            e.Property(r => r.Title).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(4000).IsRequired();
            e.Property(r => r.Category).HasMaxLength(100).IsRequired();
            e.Property(r => r.LegacyReference).HasMaxLength(50);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.Priority);
            e.HasIndex(r => r.CreatedAt);
            e.HasOne(r => r.Requester)
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.AssignedTo)
                .WithMany()
                .HasForeignKey(r => r.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Branch)
                .WithMany(b => b.ServiceRequests)
                .HasForeignKey(r => r.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Comment>(e =>
        {
            e.ToTable("Comments");
            e.Property(c => c.Body).HasMaxLength(2000).IsRequired();
            e.HasIndex(c => c.ServiceRequestId);
            e.HasOne(c => c.ServiceRequest)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RequestAssignment>(e =>
        {
            e.ToTable("Assignments");
            e.Property(a => a.Note).HasMaxLength(500);
            e.HasIndex(a => a.ServiceRequestId);
            e.HasIndex(a => a.AssigneeId);
            e.HasOne(a => a.ServiceRequest)
                .WithMany(r => r.Assignments)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Assignee)
                .WithMany()
                .HasForeignKey(a => a.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.AssignedBy)
                .WithMany()
                .HasForeignKey(a => a.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StatusHistory>(e =>
        {
            e.ToTable("StatusHistory");
            e.Property(h => h.Reason).HasMaxLength(500);
            e.HasIndex(h => h.ServiceRequestId);
            e.HasOne(h => h.ServiceRequest)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedBy)
                .WithMany()
                .HasForeignKey(h => h.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Approval>(e =>
        {
            e.ToTable("Approvals");
            e.Property(a => a.Reason).HasMaxLength(500);
            e.Property(a => a.DecisionNote).HasMaxLength(500);
            e.HasIndex(a => a.ServiceRequestId);
            e.HasOne(a => a.ServiceRequest)
                .WithMany(r => r.Approvals)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.RequestedBy)
                .WithMany()
                .HasForeignKey(a => a.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Approver)
                .WithMany()
                .HasForeignKey(a => a.ApproverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(l => l.UserName).HasMaxLength(256);
            e.Property(l => l.EntityType).HasMaxLength(100);
            e.Property(l => l.EntityId).HasMaxLength(50);
            e.Property(l => l.Details).HasMaxLength(4000);
            e.Property(l => l.IpAddress).HasMaxLength(50);
            e.HasIndex(l => l.Timestamp);
            e.HasIndex(l => l.Action);
            e.HasIndex(l => l.UserId);
        });
    }
}
