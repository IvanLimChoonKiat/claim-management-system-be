namespace ClaimsManagement.Domain.Events;

public sealed record ClaimApprovedDomainEvent(
    Guid EventId,
    Guid ClaimId,
    Guid OfficerUserId,
    decimal SettlementAmount,
    DateTime OccurredAtUtc) : IDomainEvent;
