using System.Text.Json.Serialization;

namespace BinomoBackend.Domain.Entities;

using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;

public class Position
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Symbol { get; private set; }
    public PositionType Type { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal Amount { get; private set; }
    public int Leverage { get; private set; }
    public decimal Margin { get; private set; }
    public PositionStatus Status { get; set; }
    public OrderType OrderType { get; private set; }
    public decimal? LimitPrice { get; private set; }
    public decimal? StopLoss { get; private set; }
    public decimal? TakeProfit { get; private set; }
    public decimal? ExitPrice { get; set; }
    public decimal? ProfitLoss { get; set; }
    public decimal? LiquidationPrice {get; private set;}
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public User User { get; private set; }

    private Position() { }
    
    [JsonConstructor]
    public Position(
        Guid id, Guid userId, string symbol, PositionType type,
        decimal entryPrice, decimal amount, int leverage, decimal margin,
        PositionStatus status, OrderType orderType, 
        decimal? limitPrice, decimal? stopLoss, decimal? takeProfit,
        decimal? exitPrice, decimal? profitLoss, decimal? liquidationPrice,
        DateTime createdAt, DateTime? updatedAt, DateTime? closedAt)
    {
        Id = id;
        UserId = userId;
        Symbol = symbol;
        Type = type;
        EntryPrice = entryPrice;
        Amount = amount;
        Leverage = leverage;
        Margin = margin;
        Status = status;
        OrderType = orderType;
        LimitPrice = limitPrice;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        ExitPrice = exitPrice;
        ProfitLoss = profitLoss;
        LiquidationPrice = liquidationPrice;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ClosedAt = closedAt;
    }


    public static Position CreateMarketPosition(
        Guid userId,
        string symbol,
        PositionType type,
        decimal amount,
        int leverage,
        decimal currentPrice,
        decimal liquidationPrice)
    {
        var margin = amount / leverage;
        
        return new Position
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = symbol,
            Type = type,
            EntryPrice = currentPrice,
            Amount = amount,
            Leverage = leverage,
            Margin = margin,
            Status = PositionStatus.Open,
            OrderType = OrderType.Market,
            CreatedAt = DateTime.UtcNow,
            ProfitLoss = 0,
            LiquidationPrice = liquidationPrice
        };
    }

    public static Position CreateLimitPosition(
        Guid userId,
        string symbol,
        PositionType type,
        decimal amount,
        decimal margin,
        int leverage,
        decimal? stopLoss,
        decimal? takeProfit,
        decimal limitPrice,
        decimal liquidationPrice,
        decimal entryPrice)
    {
        
        return new Position
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = symbol,
            Type = type,
            EntryPrice = entryPrice,
            Amount = amount,
            Leverage = leverage,
            Margin = margin,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            Status = PositionStatus.Open,
            OrderType = OrderType.Limit,
            LimitPrice = limitPrice,
            CreatedAt = DateTime.UtcNow,
            ProfitLoss = 0,
            LiquidationPrice = liquidationPrice
        };
    }

    public void UpdateProfitLoss(decimal currentPrice)
    {
        if (Status != PositionStatus.Open) return;

        var priceDiff = Type == PositionType.Long 
            ? currentPrice - EntryPrice 
            : EntryPrice - currentPrice;

        ProfitLoss = (priceDiff / EntryPrice) * Amount * Leverage;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close(decimal exitPrice)
    {
        if (Status != PositionStatus.Open) return;

        ExitPrice = exitPrice;
        UpdateProfitLoss(exitPrice);
        Status = PositionStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void Liquidate(decimal exitPrice)
    {
        if (Status != PositionStatus.Open) return;
        ExitPrice = exitPrice;
        UpdateProfitLoss(exitPrice);
        Status = PositionStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void SetStopLoss(decimal price)
    {
        StopLoss = price;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTakeProfit(decimal price)
    {
        TakeProfit = price;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate(decimal fillPrice)
    {
        if (Status != PositionStatus.Pending) return;

        EntryPrice = fillPrice;
        Status = PositionStatus.Open;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != PositionStatus.Pending) return;

        Status = PositionStatus.Cancelled;
        ClosedAt = DateTime.UtcNow;
    }

    public bool ShouldTriggerStopLoss(decimal currentPrice)
    {
        if (!StopLoss.HasValue || Status != PositionStatus.Open) 
            return false;

        return Type == PositionType.Long 
            ? currentPrice <= StopLoss.Value 
            : currentPrice >= StopLoss.Value;
    }

    public bool ShouldTriggerTakeProfit(decimal currentPrice)
    {
        if (!TakeProfit.HasValue || Status != PositionStatus.Open) 
            return false;

        return Type == PositionType.Long 
            ? currentPrice >= TakeProfit.Value 
            : currentPrice <= TakeProfit.Value;
    }
}
