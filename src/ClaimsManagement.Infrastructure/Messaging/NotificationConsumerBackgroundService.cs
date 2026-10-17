using System.Text.Json;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsManagement.Infrastructure.Messaging;

/// <summary>
/// The one real Kafka consumer for this assessment (docs/ARCHITECTURE.md: "deliberately the
/// smallest meaningful consumer"). Subscribes to all 5 lifecycle topics and writes a
/// NotificationLog row per event, keyed by EventId so Kafka's at-least-once redelivery is a no-op
/// on replay - this is the idempotent-consumer pattern substituting for a transactional outbox.
/// </summary>
public sealed class NotificationConsumerBackgroundService(
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationConsumerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaOptions = options.Value;
        if (!kafkaOptions.Enabled)
        {
            logger.LogInformation("Kafka disabled - notification consumer will not start.");
            return;
        }

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build();

        consumer.Subscribe(KafkaTopics.All);
        logger.LogInformation("Notification consumer subscribed to {Topics}", string.Join(", ", KafkaTopics.All));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(TimeSpan.FromSeconds(1));
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume error");
                }

                if (result is null)
                {
                    continue;
                }

                await ProcessMessageAsync(result.Message.Value, stoppingToken);
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(string json, CancellationToken cancellationToken)
    {
        Guid eventId, claimId;
        string eventType;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            eventId = root.GetProperty("eventId").GetGuid();
            claimId = root.GetProperty("claimId").GetGuid();
            eventType = root.GetProperty("eventType").GetString() ?? "Unknown";
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Received malformed event envelope, skipping: {Json}", json);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        var alreadyProcessed = await dbContext.NotificationLogs.AnyAsync(n => n.EventId == eventId, cancellationToken);
        if (alreadyProcessed)
        {
            logger.LogDebug("Event {EventId} ({EventType}) already recorded - idempotent skip.", eventId, eventType);
            return;
        }

        dbContext.NotificationLogs.Add(NotificationLog.Create(eventId, claimId, eventType, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Recorded notification for {EventType} on claim {ClaimId}", eventType, claimId);
    }
}
