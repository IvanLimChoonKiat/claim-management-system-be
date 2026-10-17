using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Application.Mapping;

public static class ClaimMapper
{
    public static ClaimSummaryDto ToSummaryDto(this Claim claim) => new(
        claim.Id,
        claim.ClaimReference,
        claim.Type,
        claim.Status,
        claim.ClaimantUserId,
        claim.AssignedOfficerUserId,
        claim.IncidentDateUtc,
        claim.EstimatedLiability,
        claim.CreatedAtUtc,
        claim.UpdatedAtUtc);

    public static ClaimDetailDto ToDetailDto(this Claim claim) => new(
        claim.Id,
        claim.ClaimReference,
        claim.Type,
        claim.Status,
        claim.ClaimantUserId,
        claim.AssignedOfficerUserId,
        claim.IncidentDateUtc,
        claim.IncidentDescription,
        claim.IncidentLocation,
        claim.VehicleRegistration,
        claim.PropertyAddress,
        claim.EstimatedLiability,
        claim.CreatedAtUtc,
        claim.UpdatedAtUtc,
        claim.Version,
        claim.StatusHistory
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new ClaimStatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAtUtc, h.Note))
            .ToList(),
        claim.InformationRequests
            .OrderBy(r => r.RequestedAtUtc)
            .Select(r => new InformationRequestDto(r.Id, r.RequestedByOfficerUserId, r.RequestText, r.RequestedAtUtc, r.ResponseText, r.RespondedAtUtc, r.Status))
            .ToList(),
        claim.Decision is null
            ? null
            : new ClaimDecisionDto(claim.Decision.OfficerUserId, claim.Decision.Outcome, claim.Decision.SettlementAmount, claim.Decision.Reason, claim.Decision.DecidedAtUtc));
}
