using System.Security.Claims;
using BinomoBackend.Application.DTOs.Payment;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BinomoBackend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly string _paymentsTopic;

    public PaymentsController(
        IKafkaProducer kafkaProducer,
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _paymentService = paymentService;
        _logger = logger;
        _paymentsTopic = configuration["Kafka:PaymentsTopic"]!;
    }

    /// <summary>
    /// Инициализация депозита (Step 1: пользователь загрузил чек)
    /// </summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromForm] decimal Amount,
        [FromForm] string CardNumber,
        [FromForm] string Provider,
        [FromForm] IFormFile receipt,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var depositRequestDto = new DepositRequestDto
        {
            Amount = Amount,
            CardNumber = CardNumber,
            Provider = Provider,
            ReceiptStream = receipt.OpenReadStream(),
            ReceiptFileName = receipt.FileName,
            ReceiptContentType = receipt.ContentType
        };

        var result = await _paymentService.DepositAsync(userId, depositRequestDto, ct);
        
        _logger.LogInformation(
            "💰 Deposit initiated: UserId={UserId}, Amount={Amount}", 
            userId, Amount);

        return Ok(new
        {
            message = "Ваш депозит отправлен на обработку. Ожидайте подтверждения.",
            eventId = result.EventId,
            status = result.Status
        });
    }
    
    [HttpPost("withdraw")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdrawal(
        [FromBody] WithdrawalRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();

            var result = await _paymentService.InitiateWithdrawalAsync(userId, request, ct);

            return Accepted(new
            {
                message = "Заявка на вывод создана. Оплатите комиссию.",
                withdrawalId = result.WithdrawalId,
                eventId = result.EventId,
                commission = result.Commission,
                status = result.Status
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error initiating withdrawal");
            return StatusCode(500, new { error = "Произошла ошибка. Попробуйте позже." });
        }
    }

    private async Task<string> UploadReceipt(IFormFile file)
    {
        // TODO: Загрузка в S3/MinIO/Azure Blob Storage
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var path = Path.Combine("uploads", "receipts", fileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        
        return $"/uploads/receipts/{fileName}";
    }
    
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}