using BankService.Domain.Entities;
using BankService.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankService.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        if (await context.Branches.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding database...");

        var roles = new[] { "Admin", "Manager", "Support", "Employee" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var branches = new List<Branch>
        {
            new() { Code = "HQ01", Name = "Headquarters", City = "New York", Address = "100 Wall Street", Phone = "+1-212-555-0100" },
            new() { Code = "NY02", Name = "Manhattan Branch", City = "New York", Address = "450 Park Avenue", Phone = "+1-212-555-0200" },
            new() { Code = "CH01", Name = "Chicago Branch", City = "Chicago", Address = "200 W Madison St", Phone = "+1-312-555-0100" },
            new() { Code = "LA01", Name = "Los Angeles Branch", City = "Los Angeles", Address = "350 S Grand Ave", Phone = "+1-213-555-0100" },
            new() { Code = "DA01", Name = "Dallas Branch", City = "Dallas", Address = "901 Main St", Phone = "+1-214-555-0100" }
        };
        await context.Branches.AddRangeAsync(branches);
        await context.SaveChangesAsync();

        var users = new List<ApplicationUser>
        {
            new() { UserName = "admin@bankportal.com", Email = "admin@bankportal.com", FirstName = "Sarah", LastName = "Mitchell", EmployeeNumber = "EMP-0001", BranchId = branches[0].Id, EmailConfirmed = true },
            new() { UserName = "manager@bankportal.com", Email = "manager@bankportal.com", FirstName = "James", LastName = "Rodriguez", EmployeeNumber = "EMP-0002", BranchId = branches[0].Id, EmailConfirmed = true },
            new() { UserName = "support1@bankportal.com", Email = "support1@bankportal.com", FirstName = "Emily", LastName = "Chen", EmployeeNumber = "EMP-0003", BranchId = branches[1].Id, EmailConfirmed = true },
            new() { UserName = "support2@bankportal.com", Email = "support2@bankportal.com", FirstName = "Michael", LastName = "Thompson", EmployeeNumber = "EMP-0004", BranchId = branches[2].Id, EmailConfirmed = true },
            new() { UserName = "employee1@bankportal.com", Email = "employee1@bankportal.com", FirstName = "Jessica", LastName = "Williams", EmployeeNumber = "EMP-0005", BranchId = branches[1].Id, EmailConfirmed = true },
            new() { UserName = "employee2@bankportal.com", Email = "employee2@bankportal.com", FirstName = "David", LastName = "Park", EmployeeNumber = "EMP-0006", BranchId = branches[3].Id, EmailConfirmed = true },
            new() { UserName = "employee3@bankportal.com", Email = "employee3@bankportal.com", FirstName = "Amanda", LastName = "Foster", EmployeeNumber = "EMP-0007", BranchId = branches[4].Id, EmailConfirmed = true }
        };

        var password = "Password@123";
        foreach (var user in users)
        {
            await userManager.CreateAsync(user, password);
        }

        await userManager.AddToRoleAsync(users[0], "Admin");
        await userManager.AddToRoleAsync(users[1], "Manager");
        await userManager.AddToRoleAsync(users[2], "Support");
        await userManager.AddToRoleAsync(users[3], "Support");
        await userManager.AddToRoleAsync(users[4], "Employee");
        await userManager.AddToRoleAsync(users[5], "Employee");
        await userManager.AddToRoleAsync(users[6], "Employee");

        var categories = new[] { "Account Services", "Loan Services", "Card Services", "Technical Support", "Fraud & Security", "Compliance", "General Inquiry" };
        var titles = new[]
        {
            "Unable to access online banking",
            "Request for account statement copy",
            "Credit card replacement needed",
            "Loan payment discrepancy",
            "Update contact information",
            "Wire transfer authorization",
            "Dispute unauthorized transaction",
            "New debit card request",
            "Mortgage rate inquiry",
            "Account closure request",
            "Password reset not working",
            "Direct deposit setup",
            "Overdraft fee refund request",
            "Joint account addition",
            "Business account upgrade"
        };

        var descriptions = new[]
        {
            "Customer reports being locked out of online banking after multiple failed login attempts. Account shows as active in the system.",
            "Customer needs a certified copy of their account statement for the past 12 months for visa application purposes.",
            "Card was damaged and needs to be replaced. Customer requests expedited shipping to their home address.",
            "Customer states their loan payment was deducted twice this month. Please investigate and process refund if confirmed.",
            "Customer has moved and needs to update their mailing address and phone number on file.",
            "Customer needs to authorize an international wire transfer of $15,000 to a beneficiary in Germany.",
            "Customer identified three unauthorized transactions totaling $847.23 on their account ending in 4521.",
            "Customer's debit card expires next month and would like a replacement with updated design.",
            "Customer inquiring about current mortgage rates for a 30-year fixed refinance of their primary residence.",
            "Customer wishes to close their savings account and transfer remaining balance to their checking account.",
            "Customer reports password reset emails are not arriving. Email address verified as correct in system.",
            "Customer needs to set up direct deposit for their new employer payroll.",
            "Customer was charged an overdraft fee but states the deposit should have covered the transaction.",
            "Customer wants to add their spouse as a joint account holder on their checking account.",
            "Small business customer wants to upgrade from personal to business account with additional features."
        };

        var random = new Random(42);
        var requests = new List<ServiceRequest>();
        var statusHistory = new List<StatusHistory>();
        var comments = new List<Comment>();
        var assignments = new List<RequestAssignment>();

        for (var i = 0; i < 35; i++)
        {
            var requester = users[random.Next(4, users.Count)];
            var status = (RequestStatus)random.Next(4);
            var priority = (RequestPriority)random.Next(4);
            var created = DateTime.UtcNow.AddDays(-random.Next(1, 60));
            var due = created.AddDays(random.Next(3, 14));

            var request = new ServiceRequest
            {
                RequestNumber = $"SR-2026-{i + 1:D5}",
                Title = titles[random.Next(titles.Length)],
                Description = descriptions[random.Next(descriptions.Length)],
                Category = categories[random.Next(categories.Length)],
                Status = status,
                Priority = priority,
                RequesterId = requester.Id,
                BranchId = requester.BranchId,
                CreatedAt = created,
                DueDate = due,
                ResolvedAt = status == RequestStatus.Resolved || status == RequestStatus.Closed ? created.AddDays(random.Next(1, 5)) : null,
                ClosedAt = status == RequestStatus.Closed ? created.AddDays(random.Next(5, 10)) : null,
                RequiresApproval = priority == RequestPriority.Critical && random.Next(2) == 0
            };

            requests.Add(request);

            statusHistory.Add(new StatusHistory
            {
                ServiceRequestId = request.Id,
                FromStatus = RequestStatus.Open,
                ToStatus = RequestStatus.Open,
                ChangedById = requester.Id,
                Reason = "Request created",
                ChangedAt = created
            });

            if (status == RequestStatus.InProgress || status == RequestStatus.Resolved || status == RequestStatus.Closed)
            {
                var assignee = users[random.Next(2, 4)];
                request.AssignedToId = assignee.Id;
                assignments.Add(new RequestAssignment
                {
                    ServiceRequestId = request.Id,
                    AssigneeId = assignee.Id,
                    AssignedById = users[1].Id,
                    AssignedAt = created.AddDays(1)
                });
                statusHistory.Add(new StatusHistory
                {
                    ServiceRequestId = request.Id,
                    FromStatus = RequestStatus.Open,
                    ToStatus = RequestStatus.InProgress,
                    ChangedById = users[1].Id,
                    Reason = $"Assigned to {assignee.FullName}",
                    ChangedAt = created.AddDays(1)
                });
            }

            if (status == RequestStatus.Resolved || status == RequestStatus.Closed)
            {
                statusHistory.Add(new StatusHistory
                {
                    ServiceRequestId = request.Id,
                    FromStatus = RequestStatus.InProgress,
                    ToStatus = RequestStatus.Resolved,
                    ChangedById = request.AssignedToId ?? users[2].Id,
                    Reason = "Issue resolved",
                    ChangedAt = request.ResolvedAt!.Value
                });
            }

            if (status == RequestStatus.Closed)
            {
                statusHistory.Add(new StatusHistory
                {
                    ServiceRequestId = request.Id,
                    FromStatus = RequestStatus.Resolved,
                    ToStatus = RequestStatus.Closed,
                    ChangedById = users[1].Id,
                    Reason = "Confirmed with customer",
                    ChangedAt = request.ClosedAt!.Value
                });
            }

            if (random.Next(3) == 0)
            {
                comments.Add(new Comment
                {
                    ServiceRequestId = request.Id,
                    AuthorId = users[random.Next(2, 4)].Id,
                    Body = "Investigating the issue. Will update shortly.",
                    IsInternal = true,
                    CreatedAt = created.AddDays(2)
                });
            }
        }

        await context.ServiceRequests.AddRangeAsync(requests);
        await context.StatusHistory.AddRangeAsync(statusHistory);
        await context.Comments.AddRangeAsync(comments);
        await context.Assignments.AddRangeAsync(assignments);
        await context.SaveChangesAsync();

        logger.LogInformation("Database seeded successfully.");
    }
}
