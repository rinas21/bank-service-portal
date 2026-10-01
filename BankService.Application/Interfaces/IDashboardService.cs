using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(string currentUserId, string role, CancellationToken ct = default);
}
