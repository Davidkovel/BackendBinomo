using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId)
    {
        try
        {

            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
                throw new Exception($"User with ID {userId} not found");
            
            return new UserDto(
                Id: user.Id,
                Name: user.Name,
                Email: user.Email,
                Balance: user.Balance,
                WalletAddress: user.WalletAddress,
                WalletType: user.WalletType
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Getting user");
            throw;
        }
    }
    
    public async Task<decimal> GetUserBalanceAsync(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
                throw new Exception($"User with ID {userId} not found");
            
            return user.Balance;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Getting user Balance");
            throw;
        }
    }

    public async Task UpdateUserBalanceAsync(Guid userId, decimal newBalance)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
                throw new Exception($"User with ID {userId} not found");
            
            user.Balance = newBalance;
            await _userRepository.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user Balance");
            throw;
        }
    }
}