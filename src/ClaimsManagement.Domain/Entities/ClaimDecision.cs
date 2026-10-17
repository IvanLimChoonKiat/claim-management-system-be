using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class ClaimDecision
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid OfficerUserId { get; private set; }
    public ClaimDecisionOutcome Outcome { get; private set; }
    public decimal? SettlementAmount { get; private set; }
    public string Reason { get; private set; } = default!;
    public DateTime DecidedAtUtc { get; private set; }

    private ClaimDecision()
    {
    }

    internal static ClaimDecision Create(
        Guid claimId, Guid officerUserId, ClaimDecisionOutcome outcome, decimal? settlementAmount, string reason, DateTime nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            OfficerUserId = officerUserId,
            Outcome = outcome,
            SettlementAmount = settlementAmount,
            Reason = reason.Trim(),
            DecidedAtUtc = nowUtc
        };
}
