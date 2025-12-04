using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class LimitOrderService : IPriceObserver
{
    private readonly ILimitOrderRepository _limitOrderRepository;
    private readonly IRedisPositionRepository _redisPositionRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LimitOrderService> _logger;

    public LimitOrderService(
        ILimitOrderRepository limitOrderRepository,
        IRedisPositionRepository redisPositionRepository,
        IPositionRepository positionRepository,
        IUnitOfWork unitOfWork,
        ILogger<LimitOrderService> logger)
    {
        _limitOrderRepository = limitOrderRepository;
        _redisPositionRepository = redisPositionRepository;
        _positionRepository = positionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task OnPriceUpdatedAsync(
        string symbol,
        decimal price,
        CancellationToken ct = default)
    {
        // Проверяем Buy ордера (исполняем если цена упала до limit price)
        var buyOrders = await _limitOrderRepository
            .GetOrdersForExecutionAsync(symbol, price, OrderSide.Buy, ct);

        // Проверяем Sell ордера (исполняем если цена выросла до limit price)
        var sellOrders = await _limitOrderRepository
            .GetOrdersForExecutionAsync(symbol, price, OrderSide.Sell, ct);

        var allToExecute = buyOrders.Concat(sellOrders).ToList();

        if (!allToExecute.Any())
        {
            return;
        }

        await Parallel.ForEachAsync(
            allToExecute,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = ct
            },
            async (order, token) => { await ExecuteOrderAsync(order, price, token); });
    }

    private async Task ExecuteOrderAsync(
        LimitOrder order,
        decimal executionPrice,
        CancellationToken ct)
    {
        try
        {
            var liquidationPrice = order.CalculateLiquidationPrice();

            var position = Position.CreateLimitPosition(
                userId: order.UserId,
                symbol: order.Symbol,
                type: order.Type,
                entryPrice: executionPrice,
                amount: order.Amount,
                margin: order.Margin,
                leverage: order.Leverage,
                stopLoss: order.StopLoss,
                takeProfit: order.TakeProfit,
                limitPrice: order.LimitPrice,
                liquidationPrice: liquidationPrice
            );

            await _unitOfWork.BeginTransactionAsync(ct);

            await _redisPositionRepository.SaveActivePositionAsync(position, ct);
            await _positionRepository.AddAsync(position, ct);
            await _limitOrderRepository.RemovePendingOrderAsync(order.Id, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Failed to execute order {OrderId}",
                order.Id);
        }
    }
}