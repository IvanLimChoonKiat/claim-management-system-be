using System.Text.Json;
using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Events;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsManagement.Infrastructure.Messaging;

/// <summary>
/// Publishes domain events to Kafka after the owning DB transaction has committed. A publish
/// failure (or Kafka being disabled/unreachable) is logged, never thrown - the REST response and
/// the database write are the source of truth and must not depend on the broker being up. This is
/// a deliberate at-least-once, no-outbox choice - see docs/ARCHITECTURE.md "Delivery guarantees".
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        if (options.Value.Enabled)
        {
            _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                Acks = Acks.All,
                MessageSendMaxRetries = 3
            }).Build();
        }
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var (topic, eventType, payload) = Describe(domainEvent);

        if (_producer is null)
        {
            _logger.LogInformation(
                "Kafka publishing disabled - skipped {EventType} for claim {ClaimId}", eventType, domainEvent.ClaimId);
            return;
        }

        var envelope = new
        {
            eventId = domainEvent.EventId,
            eventType,
            version = 1,
            occurredAtUtc = domainEvent.OccurredAtUtc,
            claimId = domainEvent.ClaimId,
            payload
        };

        try
        {
            var json = JsonSerializer.Serialize(envelope);
            var result = await _producer.ProduceAsync(
                topic,
                new Message<string, string> { Key = domainEvent.ClaimId.ToString(), Value = json },
                cancellationToken);

            _logger.LogInformation(
                "Published {EventType} for claim {ClaimId} to {Topic} partition {Partition} offset {Offset}",
                eventType, domainEvent.ClaimId, topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} for claim {ClaimId} to {Topic} - the claim change was already persisted",
                eventType, domainEvent.ClaimId, topic);
        }
    }

    private static (string Topic, string EventType, object Payload) Describe(IDomainEvent domainEvent) => domainEvent switch
    {
        ClaimSubmittedDomainEvent e => (KafkaTopics.ClaimSubmitted, "ClaimSubmitted",
            new { e.ClaimReference, ClaimType = e.ClaimType.ToString(), e.ClaimantUserId }),

        ClaimAssignedDomainEvent e => (KafkaTopics.ClaimAssigned, "ClaimAssigned",
            new { e.OfficerUserId }),

        AdditionalInformationRequestedDomainEvent e => (KafkaTopics.AdditionalInformationRequested, "AdditionalInformationRequested",
            new { e.InformationRequestId, e.OfficerUserId, e.RequestText }),

        ClaimApprovedDomainEvent e => (KafkaTopics.ClaimApproved, "ClaimApproved",
            new { e.OfficerUserId, e.SettlementAmount }),

        ClaimRejectedDomainEvent e => (KafkaTopics.ClaimRejected, "ClaimRejected",
            new { e.OfficerUserId, e.Reason }),

        _ => throw new NotSupportedException($"No Kafka mapping for domain event type '{domainEvent.GetType().Name}'.")
    };

    public void Dispose() => _producer?.Dispose();
}
