using System.ComponentModel.DataAnnotations;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Application.Dtos;

public sealed record SubmitClaimRequest(
    ClaimType Type,
    DateTime IncidentDateUtc,
    [Required, StringLength(4000, MinimumLength = 1)] string IncidentDescription,
    [Required, StringLength(500, MinimumLength = 1)] string IncidentLocation,
    string? VehicleRegistration,
    string? PropertyAddress,
    [Range(0, double.MaxValue)] decimal? EstimatedLiability);

public sealed record RequestInformationRequest([Required, StringLength(2000, MinimumLength = 1)] string RequestText);

public sealed record RespondToInformationRequest([Required, StringLength(2000, MinimumLength = 1)] string ResponseText);

public sealed record DecideClaimRequest(
    ClaimDecisionOutcome Outcome,
    decimal? SettlementAmount,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
