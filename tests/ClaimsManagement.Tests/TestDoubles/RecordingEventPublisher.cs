using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Events;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class RecordingEventPublisher : IEventPublisher
{
    public List<IDomainEvent> PublishedEvents { get; } = [];

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        PublishedEvents.Add(domainEvent);
        return Task.CompletedTask;
    }
}
