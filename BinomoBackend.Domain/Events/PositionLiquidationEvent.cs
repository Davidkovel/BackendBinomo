namespace BinomoBackend.Domain.Events;

public record PositionLiquidatedEvent
{
    public Guid PositionId { get; init; }
    public Guid UserId { get; init; }
    public string Symbol { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal LiquidationPrice { get; init; }
    public decimal ExitPrice { get; init; }
    public decimal PnL { get; init; }
    public DateTime Timestamp { get; init; }
}