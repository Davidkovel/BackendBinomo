using BinomoBackend.Domain.Enums;

namespace BinomoBackend.Domain.Entities;

public class Withdrawal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public WithdrawalStatus Status { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CommissionReceiptUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedBy { get; set; }
    
    public User User { get; set; } = null!;
}

