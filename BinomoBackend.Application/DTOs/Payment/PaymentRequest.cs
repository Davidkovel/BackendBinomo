namespace BinomoBackend.Application.DTOs.Payment;

public record DepositRequestDto
{
    public decimal Amount { get; init; }
    public string CardNumber { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;

    // Вместо IFormFile - Stream + metadata
    public Stream ReceiptStream { get; init; } = Stream.Null;
    public string ReceiptFileName { get; init; } = string.Empty;
    public string ReceiptContentType { get; init; } = string.Empty;
}

/// <summary>
/// DTO для инициализации вывода средств
/// </summary>
public record WithdrawalRequestDto
{
    public decimal Amount { get; init; }
    public string CardNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    // Вместо IFormFile - Stream + metadata
    public Stream ReceiptStream { get; init; } = Stream.Null;
    public string ReceiptFileName { get; init; } = string.Empty;
    public string ReceiptContentType { get; init; } = string.Empty;
}

/// <summary>
/// DTO для оплаты комиссии за вывод
/// </summary>
public record CommissionPaymentDto
{
    public Guid WithdrawalId { get; init; }
    public decimal CommissionAmount { get; init; }

    public Stream ReceiptStream { get; init; } = Stream.Null;
    public string ReceiptFileName { get; init; } = string.Empty;
    public string ReceiptContentType { get; init; } = string.Empty;
}

public record DepositRequest(
    decimal Amount,
    string CardNumber,
    string Provider
);

public record WithdrawalRequest(
    decimal Amount,
    string CardNumber,
    string FullName
);

public record CommissionPaymentRequest(
    Guid WithdrawalId,
    decimal CommissionAmount
);

public record DepositResult
{
    public Guid EventId { get; init; }
    public string Status { get; init; }
};

public record WithdrawalResult
{
    public Guid WithdrawalId { get; init; }
    public Guid EventId { get; init; }
    public decimal Commission { get; init; }
    public string Status { get; init; }
}

public record PayCommissionResult
{
    public Guid WithdrawalId { get; init; }
    public Guid EventId { get; init; }
    public string Status { get; init; }
}