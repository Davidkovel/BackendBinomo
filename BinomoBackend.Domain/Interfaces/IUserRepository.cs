using BinomoBackend.Domain.Entities;

namespace BinomoBackend.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByWalletAddressAsync(string walletAddress, CancellationToken ct = default);
    Task<decimal> GetUserBalanceAsync(Guid userId);
    Task UpdateUserBalanceAsync(Guid userId, decimal newBalance);
    Task<User> AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}