using ClaimsManagement.Domain.Events;

namespace ClaimsManagement.Application.Abstractions;

/// <summary>
/// Event publishing port. Implemented by Infrastructure's KafkaEventPublisher. Publish failures
/// must never throw out of here in a way that aborts the calling REST operation - see
/// docs/ARCHITECTURE.md "Delivery guarantees" for the at-least-once, no-outbox trade-off.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
