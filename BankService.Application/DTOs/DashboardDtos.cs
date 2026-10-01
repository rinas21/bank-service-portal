using BankService.Domain.Enums;

namespace BankService.Application.DTOs;

public record DashboardStatsDto(
    int TotalRequests,
    int OpenRequests,
    int InProgressRequests,
    int ResolvedRequests,
    int ClosedRequests,
    int CriticalRequests,
    int PendingApprovals,
    int OverdueRequests,
    int RequestsThisWeek,
    IReadOnlyList<StatusCountDto> RequestsByStatus,
    IReadOnlyList<PriorityCountDto> RequestsByPriority,
    IReadOnlyList<CategoryCountDto> RequestsByCategory,
    IReadOnlyList<RecentRequestDto> RecentRequests,
    IReadOnlyList<RequestActivityDto> RecentActivity);

public record StatusCountDto(string Status, int Count);
public record PriorityCountDto(string Priority, int Count);
public record CategoryCountDto(string Category, int Count);

public record RecentRequestDto(
    int Id,
    string RequestNumber,
    string Title,
    RequestStatus Status,
    RequestPriority Priority,
    string RequesterName,
    DateTime CreatedAt);

public record RequestActivityDto(
    int RequestId,
    string RequestNumber,
    string Title,
    string Action,
    string ActorName,
    DateTime Timestamp);
