using BinomoBackend.Application.DTOs.Payment;

namespace BinomoBackend.Application.Interfaces;

public interface IPaymentService
{
    Task<DepositResult> DepositAsync(Guid userId, DepositRequestDto request, CancellationToken ct);
    Task<WithdrawalResult> InitiateWithdrawalAsync(Guid userId, WithdrawalRequestDto request, CancellationToken ct);
}
