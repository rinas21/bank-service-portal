using BankService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankService.Infrastructure.Services;

public class RequestNumberService : IRequestNumberService
{
    private readonly IDbContext _dbContext;

    public RequestNumberService(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateRequestNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SR-{year}-";

        var lastNumber = await _dbContext.ServiceRequests
            .Where(r => r.RequestNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RequestNumber)
            .Select(r => r.RequestNumber)
            .FirstOrDefaultAsync(ct);

        int sequence = 1;
        if (lastNumber != null)
        {
            var lastPart = lastNumber[(prefix.Length)..];
            if (int.TryParse(lastPart, out var last))
            {
                sequence = last + 1;
            }
        }

        return $"{prefix}{sequence:D5}";
    }
}
