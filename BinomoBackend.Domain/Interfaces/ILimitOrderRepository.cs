using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.Interfaces;

public interface ILimitOrderRepository
{
    // Hot data (Redis)
    Task SavePendingOrderAsync(LimitOrder order, CancellationToken ct = default);
    Task<LimitOrder?> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<List<LimitOrder>> GetPendingOrdersBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<List<LimitOrder>> GetUserPendingOrdersAsync(Guid userId, CancellationToken ct = default);
    Task RemovePendingOrderAsync(Guid orderId, CancellationToken ct = default);
    
    // Оптимизированный метод для поиска ордеров к исполнению
    Task<List<LimitOrder>> GetOrdersForExecutionAsync(
        string symbol, 
        decimal currentPrice, 
        OrderSide side, 
        CancellationToken ct = default);
    
}