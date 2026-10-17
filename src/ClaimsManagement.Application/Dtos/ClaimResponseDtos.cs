using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Application.Dtos;

public sealed record ClaimSummaryDto(
    Guid Id,
    string ClaimReference,
    ClaimType Type,
    ClaimStatus Status,
    Guid ClaimantUserId,
    Guid? AssignedOfficerUserId,
    DateTime IncidentDateUtc,
    decimal? EstimatedLiability,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ClaimDetailDto(
    Guid Id,
    string ClaimReference,
    ClaimType Type,
    ClaimStatus Status,
    Guid ClaimantUserId,
    Guid? AssignedOfficerUserId,
    DateTime IncidentDateUtc,
    string IncidentDescription,
    string IncidentLocation,
    string? VehicleRegistration,
    string? PropertyAddress,
    decimal? EstimatedLiability,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int Version,
    IReadOnlyList<ClaimStatusHistoryDto> StatusHistory,
    IReadOnlyList<InformationRequestDto> InformationRequests,
    ClaimDecisionDto? Decision);

public sealed record ClaimStatusHistoryDto(
    ClaimStatus? FromStatus,
    ClaimStatus ToStatus,
    Guid ChangedByUserId,
    DateTime ChangedAtUtc,
    string? Note);

public sealed record InformationRequestDto(
    Guid Id,
    Guid RequestedByOfficerUserId,
    string RequestText,
    DateTime RequestedAtUtc,
    string? ResponseText,
    DateTime? RespondedAtUtc,
    InformationRequestStatus Status);

public sealed record ClaimDecisionDto(
    Guid OfficerUserId,
    ClaimDecisionOutcome Outcome,
    decimal? SettlementAmount,
    string Reason,
    DateTime DecidedAtUtc);
