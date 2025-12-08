namespace BinomoBackend.Infrastructure.Kafka;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "binomo-payment-service";
    public string PaymentsTopic { get; set; } = "binomo.payments";
    public string NotificationsTopic { get; set; } = "binomo.notifications";
    
    // For production
    public bool EnableIdempotence { get; set; } = true; // at-Least-once semantics
    public int MessageTimeoutMs { get; set; } = 10000;
    public string Acks { get; set; } = "all"; // Все реплики подтвердили
}