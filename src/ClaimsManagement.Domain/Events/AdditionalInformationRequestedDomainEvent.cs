namespace ClaimsManagement.Domain.Events;

public sealed record AdditionalInformationRequestedDomainEvent(
    Guid EventId,
    Guid ClaimId,
    Guid InformationRequestId,
    Guid OfficerUserId,
    string RequestText,
    DateTime OccurredAtUtc) : IDomainEvent;
