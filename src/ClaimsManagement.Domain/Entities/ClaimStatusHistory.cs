using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class ClaimStatusHistory
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public ClaimStatus? FromStatus { get; private set; }
    public ClaimStatus ToStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Note { get; private set; }

    private ClaimStatusHistory()
    {
    }

    internal static ClaimStatusHistory Record(
        Guid claimId, ClaimStatus? fromStatus, ClaimStatus toStatus, Guid changedByUserId, DateTime nowUtc, string? note) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = nowUtc,
            Note = note
        };
}
