using BinomoBackend.Domain.Interfaces;
using StackExchange.Redis;

namespace BinomoBackend.Infrastructure.Services;

public class RedisPriceService : IPriceService
{
    private readonly IConnectionMultiplexer _redis;
    private const string PricesKey = "prices:current";

    public RedisPriceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var price = await db.HashGetAsync(PricesKey, symbol);
        
        return price.IsNullOrEmpty ? 0 : decimal.Parse(price!);
    }

    public async Task UpdatePriceAsync(string symbol, decimal price, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.HashSetAsync(PricesKey, symbol, price.ToString());
        
        // Publish для подписчиков
        var subscriber = _redis.GetSubscriber();
        await subscriber.PublishAsync(
            "price:updates", 
            $"{symbol}:{price}");
    }
}