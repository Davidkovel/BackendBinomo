using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinomoBackend.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _tokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IWalletSignatureValidator _walletValidator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository tokenRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IWalletSignatureValidator walletValidator,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _walletValidator = walletValidator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> SignUpAsync(SignUpRequest request, CancellationToken ct)
    {
        try
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email, ct);
            if (existingUser != null)
                return Result.Failure<AuthResponse>("User already exists");

            var passwordHash = _passwordHasher.HashPassword(request.Password);
            var user = User.CreateWithPassword(request.Name, request.Email, passwordHash);

            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var response = await CreateAuthResponseAsync(user, ct);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign up for {Email}", request.Email);
            return Result.Failure<AuthResponse>("An error occurred during sign up");
        }
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email, ct);
            if (user == null || user.PasswordHash == null)
                return Result.Failure<AuthResponse>("Invalid credentials");

            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                return Result.Failure<AuthResponse>("Invalid credentials");

            user.UpdateLastLogin();
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var response = await CreateAuthResponseAsync(user, ct);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return Result.Failure<AuthResponse>("An error occurred during login");
        }
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var refreshToken = await _tokenRepository.GetByTokenAsync(request.RefreshToken, ct);
            if (refreshToken == null || !refreshToken.IsActive)
                return Result.Failure<AuthResponse>("Invalid refresh token");

            var user = await _userRepository.GetByIdAsync(refreshToken.UserId, ct);
            if (user == null)
                return Result.Failure<AuthResponse>("User not found");

            refreshToken.Revoke();
            await _tokenRepository.UpdateAsync(refreshToken, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var response = await CreateAuthResponseAsync(user, ct);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return Result.Failure<AuthResponse>("An error occurred during token refresh");
        }
    }

    public async Task<Result<AuthResponse>> WalletAuthAsync(WalletAuthRequest request, CancellationToken ct)
    {
        try
        {
            var isValid = await _walletValidator.ValidateSignatureAsync(
                request.WalletAddress, 
                request.Signature, 
                request.Message, 
                request.WalletType);

            if (!isValid)
                return Result.Failure<AuthResponse>("Invalid wallet signature");

            var user = await _userRepository.GetByWalletAddressAsync(request.WalletAddress, ct);
            
            if (user == null)
            {
                user = User.CreateWithWallet(request.WalletAddress, request.WalletType, request.Name, request.Email);
                await _userRepository.AddAsync(user, ct);
            }
            else
            {
                user.UpdateLastLogin();
                await _userRepository.UpdateAsync(user, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            var response = await CreateAuthResponseAsync(user, ct);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during wallet auth for {WalletAddress}", request.WalletAddress);
            return Result.Failure<AuthResponse>("An error occurred during wallet authentication");
        }
    }

    public async Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var token = await _tokenRepository.GetByTokenAsync(refreshToken, ct);
            if (token == null)
                return Result.Failure("Token not found");

            token.Revoke();
            await _tokenRepository.UpdateAsync(token, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token revocation");
            return Result.Failure("An error occurred during token revocation");
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken ct)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var expiresAt = _tokenService.GetRefreshTokenExpiration();

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue, expiresAt);
        await _tokenRepository.AddAsync(refreshToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            expiresAt,
            new UserDto(Id: user.Id, Name: user.Name ,Email: user.Email, Balance: user.Balance, WalletAddress: user.WalletAddress, WalletType: user.WalletType)
        );
    }
}