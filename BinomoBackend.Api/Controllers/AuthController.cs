using System.Security.Claims;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BinomoBackend.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
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
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();
        
        var user = await _userService.GetUserByIdAsync(userId);
        return Ok(user);
    }
    
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}