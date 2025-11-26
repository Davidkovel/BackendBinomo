namespace BinomoBackend.Domain.Interfaces;

public interface IPriceService
{
    Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default);
    Task UpdatePriceAsync(string symbol, decimal price, CancellationToken ct = default);
}