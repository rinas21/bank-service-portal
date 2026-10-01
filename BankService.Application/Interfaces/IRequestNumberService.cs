namespace BankService.Application.Interfaces;

public interface IRequestNumberService
{
    Task<string> GenerateRequestNumberAsync(CancellationToken ct = default);
}
