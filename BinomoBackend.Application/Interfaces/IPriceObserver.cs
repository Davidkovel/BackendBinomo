namespace BinomoBackend.Application.Interfaces;

public interface IPriceObserver
{
    Task OnPriceUpdatedAsync(string symbol, decimal price, CancellationToken ct = default);
}