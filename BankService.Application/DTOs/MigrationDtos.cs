namespace BankService.Application.DTOs;

public record CsvImportResult(
    int TotalRecords,
    int Imported,
    int Duplicates,
    int Rejected,
    IReadOnlyList<CsvImportRowResult> RowResults);

public record CsvImportRowResult(
    int RowNumber,
    string Status,
    string? LegacyReference,
    string? Message);
