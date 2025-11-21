using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.ValueObjects;

public record PositionSnapshot(
    Guid Id,
    Guid UserId,
    string Symbol,
    PositionType Type,
    decimal EntryPrice,
    decimal Amount,
    int Leverage,
    decimal Margin,
    PositionStatus Status,
    decimal ProfitLoss,
    decimal CurrentPrice,
    DateTime CreatedAt
);