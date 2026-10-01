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
        => $"{GetPrefix()}{(await GetNextSequenceAsync(ct)):D5}";

    /// <summary>
    /// Allocates <paramref name="count"/> distinct request numbers in one call so bulk
    /// operations (such as CSV migration) do not reuse a number before it is persisted.
    /// </summary>
    public async Task<IReadOnlyList<string>> GenerateRequestNumbersAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        var prefix = GetPrefix();
        var sequence = await GetNextSequenceAsync(ct);

        var numbers = new string[count];
        for (var i = 0; i < count; i++)
        {
            numbers[i] = $"{prefix}{sequence + i:D5}";
        }

        return numbers;
    }

    private static string GetPrefix() => $"SR-{DateTime.UtcNow.Year}-";

    private async Task<int> GetNextSequenceAsync(CancellationToken ct)
    {
        var prefix = GetPrefix();

        var existing = await _dbContext.ServiceRequests
            .Where(r => r.RequestNumber.StartsWith(prefix))
            .Select(r => r.RequestNumber)
            .ToListAsync(ct);

        // Only the numeric part of the sequence is considered, so a number that had to
        // carry a disambiguating suffix (for example "SR-2026-00042-1a2b") can never
        // cause the counter to restart from one.
        var highest = 0;
        foreach (var number in existing)
        {
            var suffix = number[prefix.Length..];
            var digitsEnd = 0;
            while (digitsEnd < suffix.Length && char.IsDigit(suffix[digitsEnd]))
            {
                digitsEnd++;
            }

            if (digitsEnd > 0 && int.TryParse(suffix[..digitsEnd], out var value) && value > highest)
            {
                highest = value;
            }
        }

        return highest + 1;
    }
}
