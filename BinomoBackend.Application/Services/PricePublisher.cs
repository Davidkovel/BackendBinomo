using System.Collections.Concurrent;
using BinomoBackend.Domain.Interfaces.observer;

namespace BinomoBackend.Application.Services;

public class PricePublisher : IPricePublisher
{
    private readonly ConcurrentBag<IPriceSubscriber> _subscribers = new();

    public void Subscribe(IPriceSubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    public void Unsubscribe(IPriceSubscriber subscriber)
    {
        var filtered = _subscribers.Where(s => s != subscriber).ToList();
        _subscribers.Clear();
        foreach (var s in filtered)
            _subscribers.Add(s);
    }

    public async Task Publish(string symbol, decimal price)
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.OnPriceUpdate(symbol, price);
        }
    }
}
