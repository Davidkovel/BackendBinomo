using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Events;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class LiquidationService : IPriceObserver
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LiquidationService> _logger;

    public LiquidationService(
        IPositionRepository positionRepository,
        IUserRepository userRepository,
        IServiceProvider serviceProvider,
        IUnitOfWork unitOfWork,
        ILogger<LiquidationService> logger)
    {
        _positionRepository = positionRepository;
        _userRepository = userRepository;
        _serviceProvider = serviceProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task OnPriceUpdatedAsync(string symbol, decimal price, CancellationToken ct = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var positionRedisRepository = scope.ServiceProvider.GetRequiredService<IRedisPositionRepository>();

            // Проверяем позиции
            var positions = await positionRedisRepository.GetActivePositionsBySymbolAsync(symbol, ct);

            if (!positions.Any())
            {
               // _logger.LogDebug("✅ No active positions for {Symbol}", symbol);
                return;
            }

            var positionsToLiquidate = positions
                .Where(p => ShouldLiquidate(p, price))
                .ToList();

            if (!positionsToLiquidate.Any())
            {
                //_logger.LogDebug("✅ No positions to liquidate for {Symbol}", symbol);
                return;
            }
            
            // Параллельная ликвидация
            await Parallel.ForEachAsync(
                positionsToLiquidate,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = ct
                },
                async (position, token) =>
                {
                    using var liquidationScope = _serviceProvider.CreateScope();
                    var tradingService = liquidationScope.ServiceProvider.GetRequiredService<ITradingService>();
                    var redisRepo = liquidationScope.ServiceProvider.GetRequiredService<IRedisPositionRepository>();
                    
                    await LiquidatePositionAsync(tradingService, redisRepo, position, price, token);
                });

            // _logger.LogInformation("✅ Completed liquidation processing for {Symbol}", symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during liquidation check for {Symbol}", symbol);
        }
    }

    private bool ShouldLiquidate(Position position, decimal currentPrice)
    {
        var shouldLiquidate = position.LiquidationPrice.HasValue &&
                              ((position.Type == PositionType.Long && currentPrice <= position.LiquidationPrice) ||
                               (position.Type == PositionType.Short && currentPrice >= position.LiquidationPrice));
        
        return shouldLiquidate;
    }


    private async Task<Result> LiquidatePositionAsync(
        ITradingService tradingService,
        IRedisPositionRepository redisRepo,
        Position position,
        decimal currentPrice,
        CancellationToken ct)
    {
        
        try
        {
            _logger.LogInformation(
                "⚡ Liquidating position {PositionId} for user {UserId} at {Price}",
                position.Id, position.UserId, currentPrice);

            var result = await tradingService.ClosePositionByLiquidationAsync(
                position,
                currentPrice,
                ct);

            if (result.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                Result.Failure("Position was not liquidated durring error");
            }
            
            await _unitOfWork.BeginTransactionAsync(ct);
            
            await redisRepo.RemoveActivePositionAsync(position.Id, ct);
            
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            // _logger.LogInformation(
            //     "✅ Position {PositionId} liquidated successfully",
            //     position.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Failed to liquidate position {PositionId}",
                position.Id);
            return Result.Failure("Failed to liquidate position");
        }
    }


    public async Task<Result> SetPositionHistoryAsync(Guid userId, Position position, decimal exitPrice,
        CancellationToken ct)
    {
        try
        {
            decimal profitLoss = position.ProfitLoss ?? 0;
            decimal roi = position.Margin != 0 ? profitLoss / position.Margin * 100 : 0;

            var positionHistory = new PositionsHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Symbol = position.Symbol,
                Type = position.Type,
                EntryPrice = position.EntryPrice,
                Amount = position.Amount,
                Leverage = position.Leverage,
                Margin = position.Margin,
                Status = position.Status,
                OrderType = position.OrderType,
                LimitPrice = position.LimitPrice,
                StopLoss = position.StopLoss,
                TakeProfit = position.TakeProfit,
                ExitPrice = exitPrice,
                ProfitLoss = position.ProfitLoss,
                ROI = roi,
                CloseReason = "Liquidation",
                CreatedAt = position.CreatedAt,
                ClosedAt = position.ClosedAt
            };

            await _positionRepository.AddUserPositionHistoryAsync(positionHistory, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting position history for user {UserId}", userId);
            return Result.Failure("Failed to set position history");
        }
    }

}