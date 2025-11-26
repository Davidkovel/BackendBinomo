using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using BinomoBackend.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Persistence.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly ApplicationDbContext _context;

    public PositionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Positions
            .Include(p => p.User) // JOIN с User таблицей
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<Position>> GetUserActivePositionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Positions
            .Where(p => p.UserId == userId && p.Status == PositionStatus.Open)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<PositionsHistory>> GetUserPositionHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.PositionsHistory
            .Where(p => p.UserId == userId && p.Status == PositionStatus.Closed)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<PositionsHistory> AddUserPositionHistoryAsync(PositionsHistory positionsHistory, CancellationToken ct = default)
    {
        _context.PositionsHistory.Add(positionsHistory);
        await _context.SaveChangesAsync(ct);
        return positionsHistory;
    }

    public async Task<Position> AddAsync(Position position, CancellationToken ct = default)
    {
        _context.Positions.Add(position);
        await _context.SaveChangesAsync(ct);
        return position;
    }

    public async Task UpdateAsync(Position position, CancellationToken ct = default)
    {
        var dbPosition = await _context.Positions
            .FirstOrDefaultAsync(p => p.Id == position.Id, ct);

        if (dbPosition == null)
        {
            throw new InvalidOperationException($"Position {position.Id} not found");
        }

        dbPosition.Status = position.Status;
        dbPosition.ExitPrice = position.ExitPrice;
        dbPosition.ProfitLoss = position.ProfitLoss;
        dbPosition.ClosedAt = position.ClosedAt;
        dbPosition.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        
    }

    public async Task<decimal> GetUserTotalMarginAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Positions
            .Where(p => p.UserId == userId && p.Status == PositionStatus.Open)
            .SumAsync(p => p.Margin, ct);
    }

    public async Task DeletePositionAsync(Position position, CancellationToken ct = default)
    {
        var positionToDelte = await _context.Positions
            .Where(p => p.Id == position.Id)
            .FirstOrDefaultAsync(ct);

        if (positionToDelte != null && positionToDelte.Status == PositionStatus.Closed)
        {
            _context.Positions.Remove(positionToDelte);
            await _context.SaveChangesAsync(ct);
        }
    }
    
}