using System.Globalization;
using System.Text;
using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using BankService.Domain.Entities;
using BankService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankService.Infrastructure.Services;

public class CsvImportService : ICsvImportService
{
    private readonly IDbContext _dbContext;
    private readonly IRequestNumberService _requestNumberService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(
        IDbContext dbContext,
        IRequestNumberService requestNumberService,
        ICurrentUserService currentUser,
        ILogger<CsvImportService> logger)
    {
        _dbContext = dbContext;
        _requestNumberService = requestNumberService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CsvImportResult> ImportAsync(Stream csvStream, string importedById, CancellationToken ct = default)
    {
        var results = new List<CsvImportRowResult>();
        var imported = 0;
        var duplicates = 0;
        var rejected = 0;

        using var reader = new StreamReader(csvStream, Encoding.UTF8);
        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidDataException("CSV file is empty.");
        }

        var headers = ParseCsvLine(headerLine);
        var expectedHeaders = new[] { "LegacyRef", "Title", "Description", "Category", "Priority", "Status", "BranchCode", "RequesterEmail", "DueDate" };
        var missingHeaders = expectedHeaders.Where(h => !headers.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missingHeaders.Count > 0)
        {
            throw new InvalidDataException($"CSV is missing required columns: {string.Join(", ", missingHeaders)}");
        }

        var existingRefs = await _dbContext.ServiceRequests
            .Where(r => r.LegacyReference != null)
            .Select(r => r.LegacyReference!)
            .ToListAsync(ct);
        var existingRefSet = new HashSet<string>(existingRefs, StringComparer.OrdinalIgnoreCase);

        var branches = await _dbContext.Branches.ToDictionaryAsync(b => b.Code, b => b.Id, StringComparer.OrdinalIgnoreCase, ct);
        var users = await _dbContext.Users.ToDictionaryAsync(u => u.Email!, u => u.Id, StringComparer.OrdinalIgnoreCase, ct);

        var accepted = new List<PendingImportRow>();

        var rowNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count != headers.Count)
            {
                rejected++;
                results.Add(new CsvImportRowResult(rowNumber, "Rejected", null, $"Expected {headers.Count} columns, found {fields.Count}."));
                continue;
            }

            var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                record[headers[i]] = fields[i].Trim();
            }

            var legacyRef = record.GetValueOrDefault("LegacyRef") ?? string.Empty;
            var title = record.GetValueOrDefault("Title") ?? string.Empty;
            var description = record.GetValueOrDefault("Description") ?? string.Empty;
            var category = record.GetValueOrDefault("Category") ?? string.Empty;
            var priorityRaw = record.GetValueOrDefault("Priority") ?? "Medium";
            var statusRaw = record.GetValueOrDefault("Status") ?? "Open";
            var branchCode = record.GetValueOrDefault("BranchCode") ?? string.Empty;
            var requesterEmail = record.GetValueOrDefault("RequesterEmail") ?? string.Empty;
            var dueDateRaw = record.GetValueOrDefault("DueDate") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(legacyRef) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(requesterEmail))
            {
                rejected++;
                results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, "Missing required fields (LegacyRef, Title, RequesterEmail)."));
                continue;
            }

            if (existingRefSet.Contains(legacyRef))
            {
                duplicates++;
                results.Add(new CsvImportRowResult(rowNumber, "Duplicate", legacyRef, "Legacy reference already exists."));
                continue;
            }

            if (!users.TryGetValue(requesterEmail, out var requesterId))
            {
                rejected++;
                results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, $"Requester email '{requesterEmail}' not found."));
                continue;
            }

            if (!TryParsePriority(priorityRaw, out var priority))
            {
                rejected++;
                results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, $"Invalid priority '{priorityRaw}'."));
                continue;
            }

            if (!TryParseStatus(statusRaw, out var status))
            {
                rejected++;
                results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, $"Invalid status '{statusRaw}'."));
                continue;
            }

            DateTime? dueDate = null;
            if (!string.IsNullOrWhiteSpace(dueDateRaw))
            {
                if (!DateTime.TryParse(dueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    rejected++;
                    results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, $"Invalid due date '{dueDateRaw}'."));
                    continue;
                }
                dueDate = parsedDate;
            }

            int? branchId = null;
            if (!string.IsNullOrWhiteSpace(branchCode))
            {
                if (!branches.TryGetValue(branchCode, out var bid))
                {
                    rejected++;
                    results.Add(new CsvImportRowResult(rowNumber, "Rejected", legacyRef, $"Branch code '{branchCode}' not found."));
                    continue;
                }
                branchId = bid;
            }

            existingRefSet.Add(legacyRef);
            accepted.Add(new PendingImportRow(
                rowNumber,
                legacyRef,
                title,
                description,
                string.IsNullOrWhiteSpace(category) ? "General" : category,
                priority,
                status,
                branchId,
                requesterId,
                dueDate));
        }

        // Nothing has been written yet, so the numbers are allocated as one block to
        // guarantee every accepted row receives a distinct request number.
        var requestNumbers = await _requestNumberService.GenerateRequestNumbersAsync(accepted.Count, ct);

        for (var i = 0; i < accepted.Count; i++)
        {
            var row = accepted[i];
            var requestNumber = requestNumbers[i];

            _dbContext.ServiceRequests.Add(new ServiceRequest
            {
                RequestNumber = requestNumber,
                Title = row.Title,
                Description = row.Description,
                Category = row.Category,
                Priority = row.Priority,
                Status = row.Status,
                BranchId = row.BranchId,
                RequesterId = row.RequesterId,
                DueDate = row.DueDate,
                IsMigrated = true,
                LegacyReference = row.LegacyReference
            });

            imported++;
            results.Add(new CsvImportRowResult(row.RowNumber, "Imported", row.LegacyReference, $"Created as {requestNumber}."));
        }

        await _dbContext.SaveChangesAsync(ct);

        await LogAuditAsync(AuditAction.CsvImport, "ServiceRequest", null,
            $"CSV import completed: {imported} imported, {duplicates} duplicates, {rejected} rejected.", ct);

        return new CsvImportResult(results.Count, imported, duplicates, rejected, results);
    }

    private sealed record PendingImportRow(
        int RowNumber,
        string LegacyReference,
        string Title,
        string Description,
        string Category,
        RequestPriority Priority,
        RequestStatus Status,
        int? BranchId,
        string RequesterId,
        DateTime? DueDate);

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    private static bool TryParsePriority(string value, out RequestPriority priority)
    {
        priority = RequestPriority.Medium;
        var normalized = value.Trim().ToLowerInvariant() switch
        {
            "l" or "low" => "Low",
            "m" or "med" or "medium" => "Medium",
            "h" or "high" => "High",
            "c" or "crit" or "critical" => "Critical",
            _ => value
        };
        return Enum.TryParse(normalized, true, out priority);
    }

    private static bool TryParseStatus(string value, out RequestStatus status)
    {
        status = RequestStatus.Open;
        var normalized = value.Trim().ToLowerInvariant() switch
        {
            "o" or "open" => "Open",
            "ip" or "in progress" or "in-progress" or "wip" => "InProgress",
            "r" or "resolved" or "done" or "complete" or "completed" => "Resolved",
            "cl" or "closed" => "Closed",
            _ => value
        };
        return Enum.TryParse(normalized, true, out status);
    }

    private async Task LogAuditAsync(AuditAction action, string entityType, string? entityId, string? details, CancellationToken ct)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await _dbContext.SaveChangesAsync(ct);
    }
}
