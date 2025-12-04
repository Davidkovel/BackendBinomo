namespace BinomoBackend.Domain.Interfaces.observer;

public interface IPricePublisher
{
    void Subscribe(IPriceSubscriber subscriber);
    void Unsubscribe(IPriceSubscriber subscriber);
    Task Publish(string symbol, decimal price);
}
