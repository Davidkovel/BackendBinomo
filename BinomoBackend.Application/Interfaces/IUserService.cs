using BinomoBackend.Application.DTOs.Auth;

namespace BinomoBackend.Application.Interfaces;

public interface IUserService
{
    Task<UserDto> GetUserByIdAsync(Guid userId);
    Task<decimal> GetUserBalanceAsync(Guid userId);
    Task UpdateUserBalanceAsync(Guid userId, decimal newBalance);
}
