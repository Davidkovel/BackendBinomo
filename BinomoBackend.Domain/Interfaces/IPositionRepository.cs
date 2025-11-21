using BinomoBackend.Domain.Entities;

namespace BinomoBackend.Domain.Interfaces;

public interface IPositionRepository
{
    Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Position>> GetUserActivePositionsAsync(Guid userId, CancellationToken ct = default);
    Task<List<PositionsHistory>> GetUserPositionHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<PositionsHistory> AddUserPositionHistoryAsync(PositionsHistory positionsHistory, CancellationToken ct = default);
    Task<Position> AddAsync(Position position, CancellationToken ct = default);
    Task UpdateAsync(Position position, CancellationToken ct = default);
    Task<decimal> GetUserTotalMarginAsync(Guid userId, CancellationToken ct = default);
}