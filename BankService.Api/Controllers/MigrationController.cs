using BankService.Api.Helpers;
using BankService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MigrationController : ControllerBase
{
    private readonly ICsvImportService _csvImportService;
    private readonly ICurrentUserService _currentUser;

    public MigrationController(ICsvImportService csvImportService, ICurrentUserService currentUser)
    {
        _csvImportService = csvImportService;
        _currentUser = currentUser;
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return ApiErrors.BadRequest("No file uploaded.");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ApiErrors.BadRequest("Only CSV files are supported.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _csvImportService.ImportAsync(stream, _currentUser.UserId!, ct);
        return Ok(result);
    }
}
