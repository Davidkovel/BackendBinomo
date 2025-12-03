using BinomoBackend.Application.DTOs.Payment;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Events;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IFileStorage _fileStorage;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PaymentService> _logger;

    private readonly string _paymentsTopic = "binomo.payments";

    public PaymentService(
        IUnitOfWork uow,
        IKafkaProducer kafkaProducer,
        IFileStorage fileStorage,
        IUserRepository userRepository,
        ILogger<PaymentService> logger)
    {
        _uow = uow;
        _kafkaProducer = kafkaProducer;
        _fileStorage = fileStorage;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<DepositResult> DepositAsync(Guid userId, DepositRequestDto request, CancellationToken ct)
    {
        var receiptUrl = await _fileStorage.SaveReceiptAsync(
            request.ReceiptStream,
            request.ReceiptFileName,
            request.ReceiptContentType,
            ct
        );

        var depositEvent = new DepositInitiatedEvent
        {
            UserId = userId,
            Amount = request.Amount,
            CardNumber = request.CardNumber,
            Provider = request.Provider,
            ReceiptUrl = receiptUrl
        };

        await _kafkaProducer.PublishAsync(_paymentsTopic, depositEvent, ct);

        _logger.LogInformation("Deposit initiated: UserId={UserId}, Amount={Amount}",
            userId, request.Amount);

        return new DepositResult
        {
            EventId = depositEvent.EventId,
            Status = "pending"
        };
    }

    public async Task<WithdrawalResult> InitiateWithdrawalAsync(
        Guid userId,
        WithdrawalRequestDto request,
        CancellationToken ct)
    {
        var currentBalance = await _userRepository.GetUserBalanceAsync(userId);
        var commission = request.Amount * 0.15m;
        var totalRequired = request.Amount + commission;

        if (currentBalance < totalRequired)
            throw new InvalidOperationException(
                $"Недостаточно средств. Требуется: {totalRequired:N0} UZS, Доступно: {currentBalance:N0} UZS"
            );
        
        var withdrawal = new Withdrawal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = request.Amount,
            Commission = commission,
            Status = WithdrawalStatus.CommissionPending,
            CardNumber = request.CardNumber,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };
        //

        // await _uow.SaveChangesAsync(ct);

        var evt = new WithdrawalInitiatedEvent
        {
            UserId = userId,
            Amount = request.Amount,
            Commission = commission,
            CardNumber = request.CardNumber,
            FullName = request.FullName
        };

        await _kafkaProducer.PublishAsync(_paymentsTopic, evt, ct);

        return new WithdrawalResult
        {
            WithdrawalId = withdrawal.Id,
            EventId = evt.EventId,
            Commission = commission,
            Status = "commission_pending"
        };
    }
}