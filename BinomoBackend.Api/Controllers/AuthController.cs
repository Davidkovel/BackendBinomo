using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens; // Добавьте это


namespace BinomoBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IUserService userService,
        IConfiguration config)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
        _config = config; // Инициализируйте поле
    }

    /// <summary>
    /// Sign up a new user with email and password
    /// </summary>
    [HttpPost("sign-up")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.SignUpAsync(request, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Sign up failed",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(new ProblemDetails
            {
                Title = "Login failed",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Create a new access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RefreshTokenAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(new ProblemDetails
            {
                Title = "Token refresh failed",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Authenticate using MetaMask or Phantom wallet
    /// </summary>
    [HttpPost("wallet-auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WalletAuth([FromBody] WalletAuthRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.WalletAuthAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(new ProblemDetails
            {
                Title = "Wallet authentication failed",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Revoke a refresh token
    /// </summary>
    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RevokeTokenAsync(request.RefreshToken, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Token revocation failed",
                Detail = result.Error
            });

        return NoContent();
    }

    /// <summary>
    /// Get current user info (protected endpoint example)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting current user");
        _logger.LogInformation(userId.ToString());
        if (userId == Guid.Empty)
            return Unauthorized();

        var user = await _userService.GetUserByIdAsync(userId);
        return Ok(user);
    }

    [HttpGet("debug/jwt-config")]
    [AllowAnonymous]
    public IActionResult DebugJwtConfig()
    {
        var jwtSettings = _config.GetSection("JwtSettings").Get<JwtSettings>();

        return Ok(new
        {
            secretLength = jwtSettings.Secret?.Length,
            secretPreview = jwtSettings.Secret?.Substring(0, Math.Min(10, jwtSettings.Secret?.Length ?? 0)),
            issuer = "BinomoBackend",
            audience = "BinomoBackend",
            accessTokenExpiration = jwtSettings.AccessTokenExpirationMinutes
        });
    }

    [HttpGet("debug/full-config")]
    [AllowAnonymous]
    public IActionResult DebugFullConfig()
    {
        var configSources = new Dictionary<string, object>();

        // Получаем значения из разных источников
        configSources["JwtSettings:Secret"] = _config["JwtSettings:Secret"];
        configSources["JwtSettings:Issuer"] = "BinomoBackend";
        configSources["JwtSettings:Audience"] = "BinomoBackend";
        configSources["JwtSettings:AccessTokenExpirationMinutes"] = _config["JwtSettings:AccessTokenExpirationMinutes"];

        // Получаем через GetSection
        var jwtSection = _config.GetSection("JwtSettings");
        configSources["GetSection:Secret"] = jwtSection["Secret"];
        configSources["GetSection:Expiration"] = jwtSection["AccessTokenExpirationMinutes"];

        // Получаем как объект
        var jwtSettings = _config.GetSection("JwtSettings").Get<JwtSettings>();
        configSources["Object:SecretLength"] = jwtSettings.Secret?.Length;
        configSources["Object:Expiration"] = jwtSettings.AccessTokenExpirationMinutes;

        return Ok(configSources);
    }

    [HttpPost("debug/validate-token-manually")]
    [AllowAnonymous]
    public IActionResult ValidateTokenManually([FromBody] ValidateTokenRequest request)
    {
        try
        {
            Console.WriteLine("=== MANUAL TOKEN VALIDATION ===");
            Console.WriteLine($"Token length: {request.Token.Length}");

            var jwtSettings = _config.GetSection("JwtSettings").Get<JwtSettings>();
            var tokenHandler = new JwtSecurityTokenHandler();

            var jwtToken = tokenHandler.ReadJwtToken(request.Token);

            var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true
            };

            var principal =
                tokenHandler.ValidateToken(request.Token, validationParameters, out SecurityToken validatedToken);

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value;

            return Ok(new
            {
                valid = true,
                userId,
                email,
                expires = jwtToken.ValidTo,
                issuer = jwtToken.Issuer,
                audience = string.Join(", ", jwtToken.Audiences)
            });
        }
        catch (SecurityTokenExpiredException ex)
        {
            Console.WriteLine($"❌ Token expired: {ex.Message}");
            return Ok(new { valid = false, error = "Token expired", errorType = "Expired" });
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            Console.WriteLine($"❌ Invalid issuer: {ex.Message}");
            return Ok(new { valid = false, error = ex.Message, errorType = "InvalidIssuer" });
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            Console.WriteLine($"❌ Invalid audience: {ex.Message}");
            return Ok(new { valid = false, error = ex.Message, errorType = "InvalidAudience" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Validation failed: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            return Ok(new { valid = false, error = ex.Message, errorType = ex.GetType().Name });
        }
    }

    public class ValidateTokenRequest
    {
        public string Token { get; set; }
    }

    private Guid GetUserId()
    {
        Console.WriteLine($"{ClaimTypes.NameIdentifier}:{ClaimTypes.Name}, {ClaimTypes.Email}");
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value // JWT standard
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;

        //_logger.LogInformation($"All claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}"))}");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}