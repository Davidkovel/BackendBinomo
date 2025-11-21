using BinomoBackend.Application.Common;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.DTOs.Trading;

namespace BinomoBackend.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> SignUpAsync(SignUpRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> WalletAuthAsync(WalletAuthRequest request, CancellationToken ct);
    Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct);
}