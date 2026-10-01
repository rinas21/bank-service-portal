using BankService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BankService.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("The database is not accepting connections.");
            }

            return HealthCheckResult.Healthy("The database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The database check failed.", ex);
        }
    }
}
