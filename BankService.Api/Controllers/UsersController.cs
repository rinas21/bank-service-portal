using BankService.Api.Helpers;
using BankService.Application.DTOs;
using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // Account administration is admin-only. The role is applied per action rather
    // than on the controller so that "assignable" can widen it: authorizing the
    // controller would intersect with the action attribute and keep it admin-only.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetList(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _userService.GetListAsync(search, role, isActive, page, pageSize, ct);
        return Ok(result);
    }

    // Available to every role allowed to assign requests. It returns a reduced
    // record, so it does not expose the account data reserved for admins.
    [HttpGet("assignable")]
    [Authorize(Roles = "Admin,Manager,Support")]
    public async Task<ActionResult<IReadOnlyList<AssignableUserDto>>> GetAssignable(CancellationToken ct)
    {
        var result = await _userService.GetAssignableUsersAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> GetById(string id, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(id, ct);
        return result is null ? ApiErrors.NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateAsync(id, request, ct);
        return result is null ? ApiErrors.NotFound() : Ok(result);
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
    {
        var result = await _userService.DeactivateAsync(id, ct);
        return result ? NoContent() : BadRequest();
    }

    [HttpPost("{id}/reactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reactivate(string id, CancellationToken ct)
    {
        var result = await _userService.ReactivateAsync(id, ct);
        return result ? NoContent() : BadRequest();
    }
}
