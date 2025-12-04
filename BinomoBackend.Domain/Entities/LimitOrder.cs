using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.Entities;

public class LimitOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; }
    
    public string Symbol { get; set; }
    public PositionType Type { get; set; } // Long или Short
    public OrderSide Side { get; set; } // Buy или Sell
    
    public decimal LimitPrice { get; set; } // Цена исполнения
    public decimal Amount { get; set; }
    public decimal Margin { get; set; }
    public int Leverage { get; set; }
    
    public LimitOrderStatus Status { get; set; } = LimitOrderStatus.Pending;
    
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public string? CancelReason { get; set; }
    
    public decimal CalculateLiquidationPrice()
    {
        var maintenanceMarginRate = 0.9m / Leverage;
        
        if (Type == PositionType.Long)
        {
            return LimitPrice * (1 - maintenanceMarginRate);
        }
        else
        {
            return LimitPrice * (1 + maintenanceMarginRate);
        }
    }
}