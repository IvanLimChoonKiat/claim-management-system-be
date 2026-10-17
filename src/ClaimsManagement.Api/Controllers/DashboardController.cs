using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

/// <summary>
/// Team-wide operational visibility. Open to any ClaimsOfficer - see DashboardSummaryDto for why
/// this isn't gated behind a separate "Manager" role.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
public sealed class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetSummaryAsync(cancellationToken));
}
