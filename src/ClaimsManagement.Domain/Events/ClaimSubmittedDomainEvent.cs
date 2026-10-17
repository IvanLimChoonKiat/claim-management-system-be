using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Events;

public sealed record ClaimSubmittedDomainEvent(
    Guid EventId,
    Guid ClaimId,
    string ClaimReference,
    ClaimType ClaimType,
    Guid ClaimantUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
