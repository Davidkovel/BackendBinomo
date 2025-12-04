namespace BinomoBackend.Domain.Interfaces.observer;

public interface IPriceSubscriber
{
    Task OnPriceUpdate(string symbol, decimal price);
}