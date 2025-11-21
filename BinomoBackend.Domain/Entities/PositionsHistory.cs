using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.Entities;

public class PositionsHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string Symbol { get; set; }

    public PositionType Type { get; set; }
    public PositionStatus Status { get; set; }
    public OrderType OrderType { get; set; }

    public decimal EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }

    public decimal Amount { get; set; }
    public decimal Margin { get; set; }

    public decimal? ProfitLoss { get; set; }
    public decimal? ROI { get; set; }
    public int Leverage { get; set; }

    public decimal? LimitPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }

    public string? CloseReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public PositionsHistory() { }

    public static PositionsHistory FromPosition(Position position, string reason)
    {
        return new PositionsHistory
        {
            UserId = position.UserId,
            Symbol = position.Symbol,
            Type = position.Type,
            Status = position.Status,
            OrderType = position.OrderType,
            EntryPrice = position.EntryPrice,
            ExitPrice = position.ExitPrice,
            Amount = position.Amount,
            Margin = position.Margin,
            ProfitLoss = position.ProfitLoss,
            ROI = position.Margin > 0 ? (position.ProfitLoss / position.Margin) * 100 : null,
            Leverage = position.Leverage,
            LimitPrice = position.LimitPrice,
            StopLoss = position.StopLoss,
            TakeProfit = position.TakeProfit,
            CloseReason = reason,
            CreatedAt = position.CreatedAt,
            ClosedAt = position.ClosedAt
        };
    }
}
