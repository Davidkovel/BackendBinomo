using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByWalletAddressAsync(string walletAddress, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.WalletAddress == walletAddress, ct);
    }

    public async Task<decimal> GetUserBalanceAsync(Guid userId)
    {
        try
        {
            _logger.LogDebug("💰 BALANCE DEBUG: Getting balance for user {UserId}", userId);

            var user = await _context.Users
                .AsNoTracking() // ✅ Важно для избежания конфликтов
                .FirstOrDefaultAsync(u => u.Id == userId);

            var balance = user?.Balance ?? 0;

            _logger.LogDebug("💰 BALANCE DEBUG: Retrieved balance for user {UserId}: {Balance}", userId, balance);
            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💰 BALANCE DEBUG: Error getting balance for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateUserBalanceAsync(Guid userId, decimal newBalance)
    {
        try
        {
            _logger.LogInformation("💰 BALANCE DEBUG: Updating balance for user {UserId} to {NewBalance}",
                userId, newBalance);

            // ✅ Загружаем пользователя с отслеживанием
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogError("💰 BALANCE DEBUG: User {UserId} not found", userId);
                throw new InvalidOperationException($"User {userId} not found");
            }

            _logger.LogDebug("💰 BALANCE DEBUG: User found. Old balance: {OldBalance}", user.Balance);

            // ✅ Обновляем баланс
            user.Balance = newBalance;

            _logger.LogDebug("💰 BALANCE DEBUG: Balance set to {NewBalance}, saving changes...", newBalance);

            var result = await _context.SaveChangesAsync();

            _logger.LogInformation(
                "💰 BALANCE DEBUG: Balance updated successfully for user {UserId}. Rows affected: {RowsAffected}",
                userId, result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "💰 BALANCE DEBUG: Concurrency error updating balance for user {UserId}", userId);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "💰 BALANCE DEBUG: Database error updating balance for user {UserId}", userId);

            // Логируем внутренние исключения
            var innerEx = ex.InnerException;
            while (innerEx != null)
            {
                _logger.LogError("💰 BALANCE DEBUG: Inner exception: {Message}", innerEx.Message);
                innerEx = innerEx.InnerException;
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💰 BALANCE DEBUG: Unexpected error updating balance for user {UserId}", userId);
            throw;
        }
    }


    public async Task<User> AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
        return user;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
}