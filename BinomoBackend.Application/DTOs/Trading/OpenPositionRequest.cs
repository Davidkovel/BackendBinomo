using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Application.DTOs.Trading;

public record OpenPositionRequest(
    string Symbol,
    PositionType Type,
    decimal Amount,
    int Leverage,
    decimal CurrentPrice,
    OrderType OrderType,
    decimal? LimitPrice,
    decimal? StopLoss,
    decimal? TakeProfit
);

public record ClosePositionRequest(
    Guid PositionId,
    decimal CurrentPrice
);

public record UpdatePositionRequest(
    Guid PositionId,
    decimal? StopLoss,
    decimal? TakeProfit
);

public record PositionResponse(
    Guid Id,
    string Symbol,
    PositionType Type,
    decimal EntryPrice,
    decimal Amount,
    int Leverage,
    decimal Margin,
    PositionStatus Status,
    OrderType OrderType,
    decimal? LimitPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? ProfitLoss,
    decimal? ExitPrice,
    DateTime CreatedAt,
    DateTime? ClosedAt
);

public record PositionsHistoryResponse(
    Guid Id,
    string Symbol,
    PositionType Type,
    decimal EntryPrice,
    decimal Amount,
    decimal? ROI,
    int Leverage,
    decimal Margin,
    PositionStatus Status,
    OrderType OrderType,
    decimal? LimitPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? ProfitLoss,
    decimal? ExitPrice,
    string CloseReason,
    DateTime CreatedAt,
    DateTime? ClosedAt
);


public record ActivePositionResponse(
    Guid Id,
    string Symbol,
    PositionType Type,
    decimal EntryPrice,
    decimal CurrentPrice,
    decimal Amount,
    int Leverage,
    decimal Margin,
    decimal? ProfitLoss,
    decimal? ProfitLossPercentage,
    decimal? StopLoss,
    decimal? TakeProfit,
    DateTime CreatedAt
);