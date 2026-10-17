namespace ClaimsManagement.Domain.Events;

public sealed record ClaimRejectedDomainEvent(
    Guid EventId,
    Guid ClaimId,
    Guid OfficerUserId,
    string Reason,
    DateTime OccurredAtUtc) : IDomainEvent;
