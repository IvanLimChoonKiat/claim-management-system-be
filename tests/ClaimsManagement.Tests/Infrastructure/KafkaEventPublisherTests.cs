using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Events;
using ClaimsManagement.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimsManagement.Tests.Infrastructure;

/// <summary>
/// With Kafka disabled, PublishAsync still exercises the full event -> topic/envelope mapping for
/// every domain event type (just skips the network call) - this is what guards against the
/// NotSupportedException fallback silently breaking for one of the 5 lifecycle events.
/// </summary>
public class KafkaEventPublisherTests
{
    private static KafkaEventPublisher CreateDisabledPublisher() =>
        new(Options.Create(new KafkaOptions { Enabled = false }), NullLogger<KafkaEventPublisher>.Instance);

    public static IEnumerable<object[]> AllDomainEvents()
    {
        var claimId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        yield return [new ClaimSubmittedDomainEvent(Guid.NewGuid(), claimId, "CLM-2026-TEST0001", ClaimType.Motor, Guid.NewGuid(), now)];
        yield return [new ClaimAssignedDomainEvent(Guid.NewGuid(), claimId, Guid.NewGuid(), now)];
        yield return [new AdditionalInformationRequestedDomainEvent(Guid.NewGuid(), claimId, Guid.NewGuid(), Guid.NewGuid(), "Need more info", now)];
        yield return [new ClaimApprovedDomainEvent(Guid.NewGuid(), claimId, Guid.NewGuid(), 1000m, now)];
        yield return [new ClaimRejectedDomainEvent(Guid.NewGuid(), claimId, Guid.NewGuid(), "Not covered", now)];
    }

    [Theory]
    [MemberData(nameof(AllDomainEvents))]
    public async Task PublishAsync_ForEveryLifecycleEventType_DoesNotThrow(IDomainEvent domainEvent)
    {
        var publisher = CreateDisabledPublisher();

        await publisher.PublishAsync(domainEvent, CancellationToken.None);
    }
}
