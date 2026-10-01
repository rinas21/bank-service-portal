using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly ICurrentUserService _currentUser;

    public ServiceRequestsController(IServiceRequestService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ServiceRequestSummaryDto>>> GetList(
        [FromQuery] ServiceRequestListQuery query, CancellationToken ct)
    {
        var isAdminOrManager = _currentUser.IsInRole("Admin") || _currentUser.IsInRole("Manager");
        var result = await _service.GetListAsync(query, _currentUser.UserId!, isAdminOrManager, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceRequestDetailDto>> GetById(int id, CancellationToken ct)
    {
        var isAdminOrManager = _currentUser.IsInRole("Admin") || _currentUser.IsInRole("Manager");
        var result = await _service.GetByIdAsync(id, _currentUser.UserId!, isAdminOrManager, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceRequestDetailDto>> Create(
        [FromBody] CreateServiceRequestRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, _currentUser.UserId!, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ServiceRequestDetailDto>> Update(
        int id, [FromBody] UpdateServiceRequestRequest request, CancellationToken ct)
    {
        var isAdminOrManager = _currentUser.IsInRole("Admin") || _currentUser.IsInRole("Manager");
        var result = await _service.UpdateAsync(id, request, _currentUser.UserId!, isAdminOrManager, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ServiceRequestDetailDto>> UpdateStatus(
        int id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        var isAdminOrManager = _currentUser.IsInRole("Admin") || _currentUser.IsInRole("Manager");
        var result = await _service.UpdateStatusAsync(id, request, _currentUser.UserId!, isAdminOrManager, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<ServiceRequestDetailDto>> Assign(
        int id, [FromBody] AssignRequest request, CancellationToken ct)
    {
        var result = await _service.AssignAsync(id, request, _currentUser.UserId!, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<ServiceRequestDetailDto>> AddComment(
        int id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await _service.AddCommentAsync(id, request, _currentUser.UserId!, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/approval")]
    public async Task<ActionResult<ServiceRequestDetailDto>> RequestApproval(
        int id, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.RequestApprovalAsync(id, request.Note ?? string.Empty, _currentUser.UserId!, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/approval/decision")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ServiceRequestDetailDto>> DecideApproval(
        int id, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.DecideApprovalAsync(id, request, _currentUser.UserId!, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
