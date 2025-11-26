using BinomoBackend.Domain.Entities;

namespace BinomoBackend.Domain.Interfaces;

public interface IRedisPositionRepository
{
    Task<Position?> GetActivePositionAsync(Guid positionId, CancellationToken ct = default);
    Task<List<Position>> GetActivePositionsBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<List<Position>> GetAllActivePositionsAsync(CancellationToken ct = default);
    
    Task SaveActivePositionAsync(Position position, CancellationToken ct = default);
    Task RemoveActivePositionAsync(Guid positionId, CancellationToken ct = default);
}