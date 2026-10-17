using ClaimsManagement.Api.Auth;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Route("api/officers")]
[Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
public sealed class OfficersController(ClaimQueryService queryService) : ControllerBase
{
    [HttpGet("me/claims")]
    public async Task<ActionResult<IReadOnlyList<ClaimSummaryDto>>> GetMyWorkload(CancellationToken cancellationToken) =>
        Ok(await queryService.GetOfficerWorkloadAsync(User.GetUserId(), cancellationToken));
}
