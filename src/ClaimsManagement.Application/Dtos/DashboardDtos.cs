using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Application.Dtos;

/// <summary>
/// Team-wide operational visibility (brief: "see their team's workload and performance",
/// "outstanding claims", "estimated outstanding liability exposure"). Visible to any
/// ClaimsOfficer - the brief's capability list defines only Claimant and ClaimsOfficer roles;
/// "Manager" appears only in the background narrative, not as a distinct login, so this is
/// surfaced to the officer role rather than inventing a separate Manager role (see
/// docs/ASSUMPTIONS.md).
/// </summary>
public sealed record DashboardSummaryDto(
    int TotalOpenClaims,
    decimal OutstandingLiability,
    IReadOnlyList<ClaimsByStatusDto> ClaimsByStatus,
    IReadOnlyList<OfficerWorkloadSummaryDto> OfficerWorkloads);

public sealed record ClaimsByStatusDto(ClaimStatus Status, int Count);

public sealed record OfficerWorkloadSummaryDto(
    Guid OfficerId,
    string OfficerName,
    int ActiveCount,
    int ApprovedCount,
    int RejectedCount,
    int TotalHandled);
