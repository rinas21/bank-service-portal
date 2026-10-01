using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IServiceRequestService
{
    Task<PagedResult<ServiceRequestSummaryDto>> GetListAsync(ServiceRequestListQuery query, string currentUserId, RequestActor actor, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> GetByIdAsync(int id, string currentUserId, RequestActor actor, CancellationToken ct = default);
    Task<ServiceRequestDetailDto> CreateAsync(CreateServiceRequestRequest request, string currentUserId, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> UpdateAsync(int id, UpdateServiceRequestRequest request, string currentUserId, RequestActor actor, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> UpdateStatusAsync(int id, UpdateStatusRequest request, string currentUserId, RequestActor actor, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> AssignAsync(int id, AssignRequest request, string currentUserId, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> AddCommentAsync(int id, AddCommentRequest request, string currentUserId, RequestActor actor, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> RequestApprovalAsync(int id, string reason, string currentUserId, CancellationToken ct = default);
    Task<ServiceRequestDetailDto?> DecideApprovalAsync(int id, ApprovalDecisionRequest request, string currentUserId, CancellationToken ct = default);
}
