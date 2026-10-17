namespace ClaimsManagement.Domain.Events;

public sealed record ClaimAssignedDomainEvent(
    Guid EventId,
    Guid ClaimId,
    Guid OfficerUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
