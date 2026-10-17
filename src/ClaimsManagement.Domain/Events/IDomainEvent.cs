namespace ClaimsManagement.Domain.Events;

/// <summary>
/// Marker for an event raised by an aggregate during a business operation. Collected on the
/// aggregate and published (to Kafka, via Application's IEventPublisher) only after the owning
/// transaction has committed successfully - see docs/ARCHITECTURE.md "Delivery guarantees".
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    Guid ClaimId { get; }
}
