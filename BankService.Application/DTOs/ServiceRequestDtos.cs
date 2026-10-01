using System.ComponentModel.DataAnnotations;
using BankService.Domain.Enums;

namespace BankService.Application.DTOs;

public record CreateServiceRequestRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(4000)] string Description,
    [property: Required, StringLength(100)] string Category,
    RequestPriority Priority,
    int? BranchId,
    DateTime? DueDate);

public record UpdateServiceRequestRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(4000)] string Description,
    [property: Required, StringLength(100)] string Category,
    RequestPriority Priority,
    int? BranchId,
    DateTime? DueDate);

public record UpdateStatusRequest(
    [property: Required] RequestStatus Status,
    [property: StringLength(500)] string? Reason);

public record AssignRequest(
    [property: Required] string AssigneeId,
    [property: StringLength(500)] string? Note);

public record AddCommentRequest(
    [property: Required, StringLength(2000)] string Body,
    bool IsInternal);

public record ApprovalDecisionRequest(
    [property: Required] bool Approve,
    [property: StringLength(500)] string? Note);

public record ServiceRequestListQuery(
    string? Search,
    RequestStatus? Status,
    RequestPriority? Priority,
    string? Category,
    int? BranchId,
    string? AssignedToId,
    string? RequesterId,
    bool? RequiresApproval,
    string? SortBy,
    bool Descending = false,
    int Page = 1,
    int PageSize = 10);

public record ServiceRequestSummaryDto(
    int Id,
    string RequestNumber,
    string Title,
    string Category,
    RequestStatus Status,
    RequestPriority Priority,
    string RequesterName,
    string? AssignedToName,
    string? BranchName,
    bool RequiresApproval,
    bool IsMigrated,
    DateTime CreatedAt,
    DateTime? DueDate,
    DateTime? ResolvedAt,
    int CommentCount);

public record ServiceRequestDetailDto(
    int Id,
    string RequestNumber,
    string Title,
    string Description,
    string Category,
    RequestStatus Status,
    RequestPriority Priority,
    string RequesterId,
    string RequesterName,
    int? BranchId,
    string? BranchName,
    string? AssignedToId,
    string? AssignedToName,
    bool RequiresApproval,
    bool IsMigrated,
    string? LegacyReference,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    DateTime? DueDate,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<StatusHistoryDto> StatusHistory,
    IReadOnlyList<AssignmentDto> Assignments,
    IReadOnlyList<ApprovalDto> Approvals);

public record CommentDto(
    int Id,
    string AuthorId,
    string AuthorName,
    string Body,
    bool IsInternal,
    DateTime CreatedAt);

public record StatusHistoryDto(
    int Id,
    string FromStatus,
    string ToStatus,
    string ChangedByName,
    string? Reason,
    DateTime ChangedAt);

public record AssignmentDto(
    int Id,
    string AssigneeId,
    string AssigneeName,
    string AssignedByName,
    string? Note,
    DateTime AssignedAt,
    DateTime? UnassignedAt);

public record ApprovalDto(
    int Id,
    string RequestedByName,
    string? ApproverName,
    string Status,
    string? Reason,
    string? DecisionNote,
    DateTime RequestedAt,
    DateTime? DecidedAt);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
