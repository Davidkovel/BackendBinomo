using System.Security.Claims;
using BinomoBackend.Application.DTOs.Auth;
using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace BinomoBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TradingController : ControllerBase
{
    private readonly ITradingService _tradingService;
    private readonly ILogger<TradingController> _logger;

    public TradingController(
        ITradingService tradingService,
        ILogger<TradingController> logger)
    {
        _tradingService = tradingService;
        _logger = logger;
    }

    /// <summary>
    /// Open a new trading position (Long or Short)
    /// </summary>
    [HttpPost("positions/open")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenPosition(
        [FromBody] OpenPositionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _tradingService.OpenPositionAsync(userId, request, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to open position",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Close an existing position
    /// </summary>
    [HttpPost("positions/close")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClosePosition(
        [FromBody] ClosePositionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _tradingService.ClosePositionAsync(userId, request, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to close position",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get all active positions for the current user
    /// </summary>
    [HttpGet("positions/active")]
    [ProducesResponseType(typeof(List<ActivePositionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePositions(CancellationToken ct)
    {
        _logger.LogDebug("Getting active positions");
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _tradingService.GetActivePositionsAsync(userId, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to get active positions",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get position history with pagination
    /// </summary>
    [HttpGet("positions/history")]
    [ProducesResponseType(typeof(List<PositionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPositionHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _tradingService.GetPositionHistoryAsync(userId, page, pageSize, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to get position history",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Update position Stop Loss and Take Profit
    /// </summary>
    [HttpPatch("positions/{positionId}")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePosition(
        [FromRoute] Guid positionId,
        [FromBody] UpdatePositionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _tradingService.UpdatePositionAsync(userId, request, ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to update position",
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>
    /// Open limit order
    /// </summary>
    [HttpPost("limitorder/open")]
    [ProducesResponseType(typeof(LimitOrder), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LimitOrder>> CreateOrder(
        [FromBody] CreateLimitOrderRequest request)
    {
        try
        {
            var userId = GetUserId();

            var openOrder = new LimitOrder
            {
                UserId = userId,
                Symbol = request.Symbol,
                Type = request.Type,
                Side = request.Side,
                LimitPrice = request.LimitPrice,
                Amount = request.Amount,
                Margin = request.Margin,
                Leverage = request.Leverage,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit,
                Status = LimitOrderStatus.Pending
            };

            var order = await _tradingService.CreateLimitOrderAsync(openOrder);

            return Ok(order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    

    /// <summary>
    /// get all limit orders
    /// </summary>
    [HttpGet("limit_orders")]
    [ProducesResponseType(typeof(List<LimitOrder>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<LimitOrder>>> GetMyOrders()
    {
        try
        {
            var userId = GetUserId();
            var orders = await _tradingService.GetUserLimitOrdersAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel limit order
    /// </summary>
    [HttpDelete("cancel_limit_order/{orderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CancelOrder(Guid orderId)
    {
        try
        {
            var userId = GetUserId();
            await _tradingService.CancelLimitOrderAsync(orderId, userId);
            return Ok(new { message = "Order cancelled" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}