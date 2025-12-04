namespace BinomoBackend.Domain.Events;

public record PriceUpdatedEvent
{
    public string Symbol { get; init; }
    public decimal Price { get; init; }
    public DateTime Timestamp { get; init; }
}