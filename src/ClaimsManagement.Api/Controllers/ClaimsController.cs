using ClaimsManagement.Api.Auth;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Route("api/claims")]
[Authorize]
public sealed class ClaimsController(
    ClaimSubmissionService submissionService,
    ClaimAssignmentService assignmentService,
    ClaimQueryService queryService,
    InformationRequestService informationRequestService,
    ClaimDecisionService decisionService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Claimant))]
    public async Task<ActionResult<ClaimDetailDto>> Submit(SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await submissionService.SubmitAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("mine")]
    [Authorize(Roles = nameof(UserRole.Claimant))]
    public async Task<ActionResult<IReadOnlyList<ClaimSummaryDto>>> GetMine(CancellationToken cancellationToken) =>
        Ok(await queryService.GetMyClaimsAsync(User.GetUserId(), cancellationToken));

    [HttpGet("unassigned")]
    [Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
    public async Task<ActionResult<IReadOnlyList<ClaimSummaryDto>>> GetUnassigned(CancellationToken cancellationToken) =>
        Ok(await queryService.GetUnassignedAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await queryService.GetForUserAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken));

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
    public async Task<ActionResult<ClaimDetailDto>> Assign(Guid id, CancellationToken cancellationToken) =>
        Ok(await assignmentService.AssignToOfficerAsync(id, User.GetUserId(), cancellationToken));

    [HttpPost("{id:guid}/information-requests")]
    [Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
    public async Task<ActionResult<ClaimDetailDto>> RequestInformation(Guid id, RequestInformationRequest request, CancellationToken cancellationToken) =>
        Ok(await informationRequestService.RequestInformationAsync(id, User.GetUserId(), request, cancellationToken));

    [HttpPost("{id:guid}/information-requests/{requestId:guid}/respond")]
    [Authorize(Roles = nameof(UserRole.Claimant))]
    public async Task<ActionResult<ClaimDetailDto>> RespondToInformationRequest(
        Guid id, Guid requestId, RespondToInformationRequest request, CancellationToken cancellationToken) =>
        Ok(await informationRequestService.RespondAsync(id, User.GetUserId(), requestId, request, cancellationToken));

    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = nameof(UserRole.ClaimsOfficer))]
    public async Task<ActionResult<ClaimDetailDto>> Decide(Guid id, DecideClaimRequest request, CancellationToken cancellationToken) =>
        Ok(await decisionService.DecideAsync(id, User.GetUserId(), request, cancellationToken));
}
