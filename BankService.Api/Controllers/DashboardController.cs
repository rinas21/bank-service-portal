using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _dashboardService.GetStatsAsync(_currentUser.UserId!, _currentUser.PrimaryRole, ct);
        return Ok(stats);
    }
}
