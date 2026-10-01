using BankService.Api.Helpers;
using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BranchDto>>> GetList(
        [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _branchService.GetListAsync(search, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BranchDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _branchService.GetByIdAsync(id, ct);
        return result is null ? ApiErrors.NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchRequest request, CancellationToken ct)
    {
        var result = await _branchService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BranchDto>> Update(int id, [FromBody] UpdateBranchRequest request, CancellationToken ct)
    {
        var result = await _branchService.UpdateAsync(id, request, ct);
        return result is null ? ApiErrors.NotFound() : Ok(result);
    }
}
