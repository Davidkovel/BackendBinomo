namespace BinomoBackend.Domain.Events;

public abstract record PaymentEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid UserId { get; init; }
}

/// <summary>
/// Событие: Пользователь инициировал депозит
/// </summary>
public record DepositInitiatedEvent : PaymentEvent
{
    public decimal Amount { get; init; }
    public string CardNumber { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ReceiptUrl { get; init; } = string.Empty;
}

/// <summary>
/// Событие: Депозит подтверждён администратором
/// </summary>
public record DepositConfirmedEvent : PaymentEvent
{
    public Guid TransactionId { get; init; }
    public decimal Amount { get; init; }
    public Guid ConfirmedBy { get; init; } // Admin ID
}

/// <summary>
/// Событие: Пользователь запросил вывод средств
/// </summary>
public record WithdrawalInitiatedEvent : PaymentEvent
{
    public decimal Amount { get; init; }
    public decimal Commission { get; init; }
    public string CardNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

/// <summary>
/// Событие: Комиссия оплачена (шаг 2)
/// </summary>
public record CommissionPaidEvent : PaymentEvent
{
    public Guid WithdrawalId { get; init; }
    public decimal CommissionAmount { get; init; }
    public string ReceiptUrl { get; init; } = string.Empty;
}

/// <summary>
/// Событие: Вывод средств подтверждён
/// </summary>
public record WithdrawalCompletedEvent : PaymentEvent
{
    public Guid WithdrawalId { get; init; }
    public decimal Amount { get; init; }
    public Guid ProcessedBy { get; init; } // Admin ID
}