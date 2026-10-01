using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface ICsvImportService
{
    Task<CsvImportResult> ImportAsync(Stream csvStream, string importedById, CancellationToken ct = default);
}
