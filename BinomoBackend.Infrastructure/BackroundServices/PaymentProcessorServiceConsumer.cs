using System.Transactions;
using BinomoBackend.Domain.Events;
using BinomoBackend.Domain.Interfaces;
using BinomoBackend.Infrastructure.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Model;
using Newtonsoft.Json;
using IsolationLevel = Confluent.Kafka.IsolationLevel;

namespace BinomoBackend.Infrastructure.BackroundServices;

// Kafka Consumer
public class PaymentProcessorService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentProcessorService> _logger;
    private readonly string _paymentsTopic;

    public PaymentProcessorService(
        IOptions<KafkaSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<PaymentProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _paymentsTopic = settings.Value.PaymentsTopic;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            GroupId = settings.Value.GroupId,

            // 🔥 КРИТИЧНО: Гарантия обработки
            EnableAutoCommit = false, // Ручной commit после успешной обработки
            AutoOffsetReset = AutoOffsetReset.Earliest, // Читать с начала при первом запуске

            // Изоляция транзакций
            IsolationLevel = IsolationLevel.ReadCommitted,

            // Производительность
            FetchMinBytes = 1024,
            FetchWaitMaxMs = 500
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                _logger.LogError("❌ Kafka Consumer Error: {Reason}", error.Reason))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_paymentsTopic);
        _logger.LogInformation("🎧 Payment Processor started. Listening to: {Topic}", _paymentsTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);

                    if (consumeResult?.Message == null)
                        continue;

                    _logger.LogInformation(
                        "📥 Received event: Partition={Partition}, Offset={Offset}",
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value
                    );

                    // Десериализуем событие
                    var @event = JsonConvert.DeserializeObject<PaymentEvent>(
                        consumeResult.Message.Value,
                        new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All
                        }
                    );

                    if (@event == null)
                    {
                        _logger.LogWarning("⚠️ Failed to deserialize event");
                        continue;
                    }

                    // Обрабатываем событие
                    await ProcessEventAsync(@event, stoppingToken);

                    // ✅ Коммитим offset ТОЛЬКО после успешной обработки
                    _consumer.Commit(consumeResult);

                    _logger.LogInformation(
                        "✅ Event processed and committed: {EventType} | EventId={EventId}",
                        @event.GetType().Name,
                        @event.EventId
                    );
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "❌ Error consuming message");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing event");
                    // НЕ коммитим offset - событие будет обработано заново
                }
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task ProcessEventAsync(PaymentEvent @event, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        switch (@event)
        {
            case DepositInitiatedEvent depositEvent:
                await HandleDepositConfirmed(depositEvent, ct);
                break;

            case WithdrawalInitiatedEvent withdrawalEvent:
                await HandleWithdrawal(withdrawalEvent, ct);
                break;

            default:
                _logger.LogWarning("⚠️ Unknown event type: {EventType}", @event.GetType().Name);
                break;
        }
    }

    private async Task HandleDepositConfirmed(
        DepositInitiatedEvent @event,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "✅ Confirming deposit: Amount={Amount}",
            @event.Amount
        );
        using var scope = _serviceProvider.CreateScope();

        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var currentBalance = await userRepository.GetUserBalanceAsync(@event.UserId);
            var newBalance = currentBalance + @event.Amount;
            
            await userRepository.UpdateUserBalanceAsync(@event.UserId, newBalance);
            //await userRepository.UpdateUserBalancePessimisticAsync(@event.UserId, newBalance);
            
            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "✅ Deposit confirmed: UserId={UserId}, NewBalance={NewBalance}",
                @event.UserId, newBalance
            );
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task HandleWithdrawal(WithdrawalInitiatedEvent withdrawalEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "✅ Confirming withdrawal event: Amount={Amount}",
            @withdrawalEvent.Amount
        );
        using var scope = _serviceProvider.CreateScope();

        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var currentBalance = await userRepository.GetUserBalanceAsync(@withdrawalEvent.UserId);
            var newBalance = currentBalance - @withdrawalEvent.Amount;
            
            await userRepository.UpdateUserBalanceAsync(@withdrawalEvent.UserId, newBalance);
            //await userRepository.UpdateUserBalancePessimisticAsync(@withdrawalEvent.UserId, newBalance);

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "✅ Withdrawal confirmed: UserId={UserId}, NewBalance={NewBalance}",
                @withdrawalEvent.UserId, newBalance
            );
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }


    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}