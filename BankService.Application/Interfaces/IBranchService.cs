using BankService.Application.DTOs;

namespace BankService.Application.Interfaces;

public interface IBranchService
{
    Task<PagedResult<BranchDto>> GetListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<BranchDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default);
    Task<BranchDto?> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken ct = default);
}
