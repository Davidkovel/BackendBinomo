using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Services;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Application.Interfaces;

public interface ITradingService
{
    Task<Result<PositionResponse>> OpenPositionAsync(Guid userId, OpenPositionRequest request, CancellationToken ct);
    Task<Result<PositionResponse>> ClosePositionAsync(Guid userId, ClosePositionRequest request, CancellationToken ct);
    Task<Result> ClosePositionByLiquidationAsync(Position position, decimal currentPrice, CancellationToken ct);
    Task<Result<PositionResponse>> UpdatePositionAsync(Guid userId, UpdatePositionRequest request, CancellationToken ct);
    Task<Result<List<ActivePositionResponse>>> GetActivePositionsAsync(Guid userId, CancellationToken ct);
    Task<Result<List<PositionsHistoryResponse>>> GetPositionHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct);

    Task<LimitOrder> CreateLimitOrderAsync(LimitOrder order, CancellationToken ct = default);
    Task<List<LimitOrder>> GetUserLimitOrdersAsync(Guid userId, CancellationToken ct = default);
    Task CancelLimitOrderAsync(Guid orderId, Guid userId, CancellationToken ct = default);
}

