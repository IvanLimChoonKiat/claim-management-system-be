namespace ClaimsManagement.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>
    /// Master switch. When false, the producer no-ops (logs and returns) and the notification
    /// consumer never starts - lets the API and ordinary `dotnet run` work with no broker present.
    /// </summary>
    public bool Enabled { get; set; }

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ConsumerGroupId { get; set; } = "claims-notification-consumer";
}
