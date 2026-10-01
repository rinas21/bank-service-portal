namespace BankService.Application.Interfaces;

public interface IRequestNumberService
{
    Task<string> GenerateRequestNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// Allocates a block of distinct request numbers for bulk operations so that a
    /// number is never handed out twice before it has been persisted.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateRequestNumbersAsync(int count, CancellationToken ct = default);
}
