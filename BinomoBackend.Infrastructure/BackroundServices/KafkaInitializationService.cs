using BinomoBackend.Domain.Events;
using BinomoBackend.Infrastructure.Kafka;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BinomoBackend.Infrastructure.BackroundServices;

/// <summary>
/// Инициализирует Kafka топики и отправляет тестовое сообщение при старте
/// </summary>
public class KafkaInitializationService : BackgroundService
{
    private readonly ILogger<KafkaInitializationService> _logger;
    private readonly KafkaSettings _settings;

    public KafkaInitializationService(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaInitializationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔧 Kafka Initialization Service starting...");

        try
        {
            // Ждем пока Kafka станет доступна
            if (!await WaitForKafkaAsync(stoppingToken))
            {
                _logger.LogError("❌ Kafka not available. Skipping initialization.");
                return;
            }

            // Создаем или проверяем топики
            await EnsureTopicsExistAsync(stoppingToken);

            // Отправляем тестовое сообщение
            await SendTestMessageAsync(stoppingToken);

            _logger.LogInformation("✅ Kafka initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Kafka initialization failed");
        }

        // После инициализации этот сервис завершает работу
    }

    private async Task<bool> WaitForKafkaAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("🔍 Checking Kafka availability (attempt {Attempt}/{Max})...",
                    attempt, maxAttempts);

                using var adminClient = new AdminClientBuilder(
                    new AdminClientConfig
                    {
                        BootstrapServers = _settings.BootstrapServers,
                        SocketTimeoutMs = 5000
                    }).Build();

                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));

                _logger.LogInformation("✅ Kafka is available. Brokers: {Count}",
                    metadata.Brokers.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "⚠️ Kafka not available (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, maxAttempts, delay.TotalSeconds);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        return false;
    }

    private async Task EnsureTopicsExistAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(
                new AdminClientConfig
                {
                    BootstrapServers = _settings.BootstrapServers
                }).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            // Проверяем payments topic
            await EnsureTopicExistsAsync(
                adminClient,
                _settings.PaymentsTopic,
                partitions: 3,
                cancellationToken);

            // Проверяем notifications topic
            await EnsureTopicExistsAsync(
                adminClient,
                _settings.NotificationsTopic,
                partitions: 3,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to ensure topics exist");
            throw;
        }
    }

    private async Task EnsureTopicExistsAsync(
        IAdminClient adminClient,
        string topicName,
        int partitions,
        CancellationToken cancellationToken)
    {
        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            var topicExists = metadata.Topics.Any(t => t.Topic == topicName);

            if (topicExists)
            {
                _logger.LogInformation("✅ Topic '{Topic}' already exists", topicName);
                return;
            }

            _logger.LogInformation("📝 Creating topic '{Topic}'...", topicName);

            var topicSpec = new TopicSpecification
            {
                Name = topicName,
                NumPartitions = partitions,
                ReplicationFactor = 1
            };

            await adminClient.CreateTopicsAsync(new[] { topicSpec });

            _logger.LogInformation("✅ Topic '{Topic}' created successfully", topicName);

            // Даем Kafka время на создание топика
            await Task.Delay(2000, cancellationToken);
        }
        catch (CreateTopicsException ex)
        {
            _logger.LogError(ex, "❌ Failed to create topic '{Topic}': {Reason}",
                topicName, ex.Results[0].Error.Reason);
            throw;
        }
    }

    private async Task SendTestMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("📤 Sending test message to '{Topic}'...", _settings.PaymentsTopic);

            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                EnableIdempotence = _settings.EnableIdempotence,
                Acks = _settings.Acks == "all" ? Acks.All : Acks.Leader,
                MessageTimeoutMs = _settings.MessageTimeoutMs
            };

            using var producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, error) =>
                    _logger.LogError("❌ Kafka Producer Error: {Reason}", error.Reason))
                .Build();

            // Создаем тестовое событие
            var testEvent = new DepositInitiatedEvent
            {
                Amount = 100.00m,
                CardNumber = "asdasd",
                Provider = "asdas",
                ReceiptUrl = "C://Users//David//OneDrive//Зображення//Screenshots//Снимок экрана 2024-10-07 184703.png"
                
            };

            var messageJson = JsonConvert.SerializeObject(testEvent, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = Formatting.None
            });

            var message = new Message<string, string>
            {
                Key = testEvent.EventId.ToString(),
                Value = messageJson,
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(nameof(DepositInitiatedEvent)) },
                    { "test-message", System.Text.Encoding.UTF8.GetBytes("true") }
                }
            };

            var result = await producer.ProduceAsync(_settings.PaymentsTopic, message, cancellationToken);

            _logger.LogInformation(
                "✅ Test message sent successfully: Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);

            // Даем время на доставку
            await Task.Delay(1000, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send test message");
            throw;
        }
    }
}

