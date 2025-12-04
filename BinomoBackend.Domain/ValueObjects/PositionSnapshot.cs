using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.ValueObjects;

public class PositionSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Symbol { get; set; }
    public PositionType Type { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal Margin { get; set; }
    public int Leverage { get; set; }
    public decimal LiquidationPrice { get; set; }
    
    
    public static decimal CalculateLiquidationPrice(
        decimal entryPrice, 
        int leverage, 
        PositionType type)
    {
        // Long: liquidationPrice = entryPrice * (1 - 1/leverage * 0.9)
        // Short: liquidationPrice = entryPrice * (1 + 1/leverage * 0.9)
        var maintenanceMarginRate = 0.9m / leverage;
        
        return type == PositionType.Long
            ? entryPrice * (1 - maintenanceMarginRate)
            : entryPrice * (1 + maintenanceMarginRate);
    }
}