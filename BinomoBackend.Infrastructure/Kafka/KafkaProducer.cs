using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Events;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BinomoBackend.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly KafkaSettings _settings;

    public KafkaProducer(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        _settings = settings.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            
            // 🔥 КРИТИЧНО: Гарантированная доставка
            EnableIdempotence = _settings.EnableIdempotence, // Exactly-once
            Acks = Acks.All, // Все реплики подтвердили запись
            MaxInFlight = 5, // Макс. одновременных запросов
            
            // Retry policy
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
            
            // Производительность
            CompressionType = CompressionType.Snappy,
            LingerMs = 5, // Батчинг сообщений
            BatchSize = 16384,
            
            // Таймауты
            MessageTimeoutMs = _settings.MessageTimeoutMs
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => 
                _logger.LogError("❌ Kafka Producer Error: {Reason}", error.Reason))
            .Build();

        _logger.LogInformation("✅ Kafka Producer initialized");
    }

    public async Task PublishAsync<TEvent>(
        string topic, 
        TEvent @event, 
        CancellationToken ct = default) 
        where TEvent : PaymentEvent
    {
        try
        {
            var eventJson = JsonConvert.SerializeObject(@event, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All // Сохраняем тип события
            });

            // 🔑 Ключ партиционирования = UserId (все события пользователя в одну партицию)
            var message = new Message<string, string>
            {
                Key = @event.UserId.ToString(),
                Value = eventJson,
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(@event.GetType().Name) },
                    { "event-id", System.Text.Encoding.UTF8.GetBytes(@event.EventId.ToString()) },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(@event.Timestamp.ToString("O")) }
                }
            };

            var result = await _producer.ProduceAsync(topic, message, ct);

            _logger.LogInformation(
                "📤 Event published: {EventType} | EventId={EventId} | Partition={Partition} | Offset={Offset}",
                @event.GetType().Name,
                @event.EventId,
                result.Partition.Value,
                result.Offset.Value
            );
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, 
                "❌ Failed to publish event: {EventType} | Error={Error}", 
                @event.GetType().Name, 
                ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}