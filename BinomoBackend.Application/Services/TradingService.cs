using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Interfaces;
using BinomoBackend.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class TradingService : ITradingService
{
    private readonly IPositionRepository _positionRepository;
    private readonly IRedisPositionRepository _redisPositionRepository;
    private readonly ILimitOrderRepository _limitOrderRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TradingService> _logger;

    private const decimal MinAmount = 10m;
    private const decimal MaxAmount = 1000000m;
    private const int MinLeverage = 1;
    private const int MaxLeverage = 1000;

    public TradingService(
        IPositionRepository positionRepository,
        IRedisPositionRepository redisPositionRepository,
        ILimitOrderRepository limitOrderRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<TradingService> logger)
    {
        _positionRepository = positionRepository;
        _redisPositionRepository = redisPositionRepository;
        _limitOrderRepository = limitOrderRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PositionResponse>> OpenPositionAsync(
        Guid userId,
        OpenPositionRequest request,
        CancellationToken ct)
    {
        try
        {
            var validationResult = ValidateOpenPositionRequest(request);
            if (validationResult.IsFailure)
                return Result.Failure<PositionResponse>(validationResult.Error);

            var user = await _userRepository.GetByIdAsync(userId, ct);
            if (user == null)
                return Result.Failure<PositionResponse>("User not found");

            var requiredMargin = request.Amount / request.Leverage;
            var currentMargin = await _positionRepository.GetUserTotalMarginAsync(userId, ct);
            var availableBalance = 10000m;

            if (currentMargin + requiredMargin > availableBalance)
                return Result.Failure<PositionResponse>("Insufficient balance");

            Position position;

            position = Position.CreateMarketPosition(
                userId,
                request.Symbol,
                request.Type,
                request.Amount,
                request.Leverage,
                request.CurrentPrice,
                request.LiquidationPrice
            );

            if (request.StopLoss.HasValue)
                position.SetStopLoss(request.StopLoss.Value);

            if (request.TakeProfit.HasValue)
                position.SetTakeProfit(request.TakeProfit.Value);

            await _unitOfWork.BeginTransactionAsync(ct);

            await _redisPositionRepository.SaveActivePositionAsync(position, ct);
            await _positionRepository.AddAsync(position, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);

            return Result.Success(MapToPositionResponse(position));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Error opening position for user {UserId}", userId);
            return Result.Failure<PositionResponse>("Failed to open position");
        }
    }

    public async Task<Result<PositionResponse>> ClosePositionAsync(
        Guid userId,
        ClosePositionRequest request,
        CancellationToken ct)
    {
        try
        {
            var position = await _positionRepository.GetByIdAsync(request.PositionId, ct);

            if (position == null)
                return Result.Failure<PositionResponse>("Position not found");

            if (position.UserId != userId)
                return Result.Failure<PositionResponse>("Unauthorized");

            if (position.Status != PositionStatus.Open)
                return Result.Failure<PositionResponse>("Position is not open");

            await _unitOfWork.BeginTransactionAsync(ct);

            position.Close(request.CurrentPrice);
            await _positionRepository.UpdateAsync(position, ct);

            var balanceUpdateResult = await UpdateUserBalanceAsync(userId, position.ProfitLoss ?? 0, ct);
            if (!balanceUpdateResult.IsSuccess)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                return Result.Failure<PositionResponse>("Failed to update user balance");
            }

            var setHistoryResult = await SetPositionHistoryAsync(userId, position, request.CurrentPrice, ct);
            if (!setHistoryResult.IsSuccess)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                return Result.Failure<PositionResponse>("Failed to save position history");
            }

            await _positionRepository.DeletePositionAsync(position, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "Position closed: {PositionId} with P/L: {ProfitLoss}",
                position.Id,
                position.ProfitLoss,
                position.ExitPrice
            );

            return Result.Success(MapToPositionResponse(position));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Error closing position {PositionId}", request.PositionId);
            return Result.Failure<PositionResponse>("Failed to close position");
        }
    }

    public async Task<Result> ClosePositionByLiquidationAsync(Position position, decimal currentPrice,
        CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);

        position.Liquidate(currentPrice);
        await _positionRepository.UpdateAsync(position, ct);

        var balanceUpdateResult = await LiquidateFullBalanceAsync(position.UserId, ct);
        if (!balanceUpdateResult.IsSuccess)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return Result.Failure<PositionResponse>("Failed to update user balance");
        }

        var setHistoryResult =
            await SetPositionHistoryAsync(position.UserId, position, currentPrice, ct, "Liquidation");
        if (!setHistoryResult.IsSuccess)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            _logger.LogError("Failed to save position history");
            return Result.Failure<PositionResponse>("Failed to save position history");
        }


        await _positionRepository.DeletePositionAsync(position, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.CommitTransactionAsync(ct);

        return Result.Success();
    }

    public async Task<Result<List<ActivePositionResponse>>> GetActivePositionsAsync(
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var positions = await _positionRepository.GetUserActivePositionsAsync(userId, ct);

            if (!positions.Any())
                return Result.Success(new List<ActivePositionResponse>());

            var result = positions.Select(p =>
            {
                var profitLossPercentage = (p.ProfitLoss / p.Margin) * 100;

                return new ActivePositionResponse(
                    p.Id,
                    p.Symbol,
                    p.Type,
                    p.EntryPrice,
                    0,
                    p.Amount,
                    p.Leverage,
                    p.Margin,
                    p.ProfitLoss,
                    profitLossPercentage,
                    p.StopLoss,
                    p.TakeProfit,
                    p.CreatedAt
                );
            }).ToList();

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active positions for user {UserId}", userId);
            return Result.Failure<List<ActivePositionResponse>>("Failed to get active positions");
        }
    }

    public async Task<Result<List<PositionsHistoryResponse>>> GetPositionHistoryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        try
        {
            var positionsHistory = await _positionRepository.GetUserPositionHistoryAsync(userId, page, pageSize, ct);

            if (!positionsHistory.Any())
                return Result.Success(new List<PositionsHistoryResponse>());

            var result = positionsHistory.Select(ph =>
            {
                return new PositionsHistoryResponse(
                    Id: ph.Id,
                    Symbol: ph.Symbol,
                    Type: ph.Type,
                    EntryPrice: ph.EntryPrice,
                    Amount: ph.Amount,
                    Leverage: ph.Leverage,
                    Margin: ph.Margin,
                    Status: ph.Status,
                    OrderType: ph.OrderType,
                    LimitPrice: ph.LimitPrice,
                    StopLoss: ph.StopLoss,
                    TakeProfit: ph.TakeProfit,
                    ProfitLoss: ph.ProfitLoss,
                    ExitPrice: ph.ExitPrice,
                    ROI: ph.ROI,
                    CloseReason: ph.CloseReason,
                    CreatedAt: ph.CreatedAt,
                    ClosedAt: ph.ClosedAt
                );
            }).ToList();

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history positions for user {UserId}", userId);
            return Result.Failure<List<PositionsHistoryResponse>>("Failed to get history positions");
        }
    }

    public async Task<Result> SetPositionHistoryAsync(Guid userId, Position position, decimal exitPrice,
        CancellationToken ct, string closeReason = "Close manual")
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
                CloseReason = closeReason,
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

    private Result ValidateOpenPositionRequest(OpenPositionRequest request)
    {
        if (request.Amount < MinAmount || request.Amount > MaxAmount)
            return Result.Failure($"Amount must be between {MinAmount} and {MaxAmount}");

        if (request.Leverage < MinLeverage || request.Leverage > MaxLeverage)
            return Result.Failure($"Leverage must be between {MinLeverage} and {MaxLeverage}");

        return Result.Success();
    }

    private PositionResponse MapToPositionResponse(PositionsHistory ph)
    {
        return new PositionResponse(
            ph.Id,
            ph.Symbol,
            ph.Type,
            ph.EntryPrice,
            ph.Amount,
            ph.Leverage,
            ph.Margin,
            ph.Status,
            ph.OrderType,
            ph.LimitPrice,
            ph.StopLoss,
            ph.TakeProfit,
            ph.ProfitLoss ?? 0,
            ph.ExitPrice,
            ph.CreatedAt,
            ph.ClosedAt
        );
    }

    private PositionResponse MapToPositionResponse(Position position)
    {
        return new PositionResponse(
            position.Id,
            position.Symbol,
            position.Type,
            position.EntryPrice,
            position.Amount,
            position.Leverage,
            position.Margin,
            position.Status,
            position.OrderType,
            position.LimitPrice,
            position.StopLoss,
            position.TakeProfit,
            position.ProfitLoss,
            position.ExitPrice,
            position.CreatedAt,
            position.ClosedAt
        );
    }

    public Task<Result<PositionResponse>> UpdatePositionAsync(Guid userId, UpdatePositionRequest request,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task<Result> UpdateUserBalanceAsync(Guid userId, decimal profitLoss, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("💰 BALANCE DEBUG: Starting balance update for user {UserId}, P/L: {ProfitLoss}",
                userId, profitLoss);

            var currentBalance = await _userRepository.GetUserBalanceAsync(userId);
            _logger.LogInformation("💰 BALANCE DEBUG: Current balance for user {UserId}: {CurrentBalance}",
                userId, currentBalance);

            decimal newBalance = currentBalance + profitLoss;
            _logger.LogInformation("💰 BALANCE DEBUG: Calculation: {CurrentBalance} + {ProfitLoss} = {NewBalance}",
                currentBalance, profitLoss, newBalance);

            if (newBalance < 0)
            {
                _logger.LogWarning(
                    "💰 BALANCE DEBUG: Insufficient balance. User {UserId} would have negative balance: {NewBalance}",
                    userId, newBalance);
                return Result.Failure("Insufficient balance to cover losses");
            }

            _logger.LogInformation("💰 BALANCE DEBUG: New balance is valid: {NewBalance}", newBalance);

            _logger.LogInformation("💰 BALANCE DEBUG: Calling UpdateUserBalanceAsync for user {UserId}", userId);
            await _userRepository.UpdateUserBalanceAsync(userId, newBalance);

            var updatedBalance = await _userRepository.GetUserBalanceAsync(userId);
            _logger.LogInformation("💰 BALANCE DEBUG: Balance after update for user {UserId}: {UpdatedBalance}",
                userId, updatedBalance);

            if (Math.Abs(updatedBalance - newBalance) > 0.01m)
            {
                _logger.LogError("💰 BALANCE DEBUG: Balance update mismatch! Expected: {Expected}, Actual: {Actual}",
                    newBalance, updatedBalance);
                return Result.Failure("Balance update failed - values don't match");
            }

            _logger.LogInformation("💰 BALANCE DEBUG: Balance successfully updated for user {UserId}", userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💰 BALANCE DEBUG: Error updating balance for user {UserId}", userId);
            return Result.Failure("Failed to update balance");
        }
    }

    private async Task<Result> LiquidateFullBalanceAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var currentBalance = await _userRepository.GetUserBalanceAsync(userId);

            decimal newBalance = 0;

            await _userRepository.UpdateUserBalanceAsync(userId, newBalance);

            var updatedBalance = await _userRepository.GetUserBalanceAsync(userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error liquidating balance for user {UserId}", userId);
            return Result.Failure("Failed to liquidate balance");
        }
    }

    public async Task<LimitOrder> CreateLimitOrderAsync(
        LimitOrder order,
        CancellationToken ct = default)
    {
        await _limitOrderRepository.SavePendingOrderAsync(order, ct);
        
        return order;
    }

    public async Task<List<LimitOrder>> GetUserLimitOrdersAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _limitOrderRepository.GetUserPendingOrdersAsync(userId, ct);
    }

    public async Task CancelLimitOrderAsync(
        Guid orderId,
        Guid userId,
        CancellationToken ct = default)
    {
        var order = await _limitOrderRepository.GetOrderByIdAsync(orderId, ct);

        if (order == null)
        {
            throw new InvalidOperationException("Order not found");
        }

        if (order.UserId != userId)
        {
            throw new UnauthorizedAccessException("Not your order");
        }

        if (order.Status != LimitOrderStatus.Pending)
        {
            throw new InvalidOperationException("Order cannot be cancelled");
        }

        await _limitOrderRepository.RemovePendingOrderAsync(orderId, ct);
    }


    public Task<Result<decimal>> GetUserBalanceAsync(Guid userId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}