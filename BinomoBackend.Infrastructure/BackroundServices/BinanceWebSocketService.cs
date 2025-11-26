using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Infrastructure.BackroundServices;

public class BinanceWebSocketService : BackgroundService
{
    private readonly ILogger<BinanceWebSocketService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPriceService _priceService;

    private const string StreamUrl = "wss://fstream.binance.com/stream?streams=" +
                                     "btcusdt@markPrice/" +
                                     "ethusdt@markPrice/" +
                                     "bnbusdt@markPrice/" +
                                     "solusdt@markPrice/" +
                                     "adausdt@markPrice/" +
                                     "xrpusdt@markPrice";

    private ClientWebSocket? _webSocket;

    public BinanceWebSocketService(
        ILogger<BinanceWebSocketService> logger,
        IServiceProvider serviceProvider,
        IPriceService priceService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _priceService = priceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Binance WebSocket service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket error");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ConnectAndProcessAsync(CancellationToken ct)
    {
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(new Uri(StreamUrl), ct);

        _logger.LogInformation("✅ Connected to Binance WebSocket for BTC, ETH, BNB, SOL, ADA, XRP");

        var buffer = new byte[1024 * 1024];

        while (_webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(message, ct);
            }
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken ct)
    {
        try
        {
            var data = JsonDocument.Parse(message);

            if (!data.RootElement.TryGetProperty("data", out var dataObj))
                return;

            var symbol = dataObj.GetProperty("s").GetString();
            var priceStr = dataObj.GetProperty("p").GetString();
            
            if (symbol == null || priceStr == null)
                return;

            var price = decimal.Parse(priceStr, CultureInfo.InvariantCulture);
            
            // Сохраняем в Redis и публикуем
            await _priceService.UpdatePriceAsync(symbol, price, ct);
            
            // Уведомляем наблюдателей через scope
            using var scope = _serviceProvider.CreateScope();
            var observers = scope.ServiceProvider.GetServices<IPriceObserver>();
            
            foreach (var observer in observers)
            {
                await observer.OnPriceUpdatedAsync(symbol, price, ct);
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Service stopping",
                ct);
        }

        _webSocket?.Dispose();
        await base.StopAsync(ct);
    }
}