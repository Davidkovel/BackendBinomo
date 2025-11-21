using BinomoBackend.Domain.Entities;

namespace BinomoBackend.Application.Interfaces;

public interface IWalletSignatureValidator
{
    Task<bool> ValidateSignatureAsync(string walletAddress, string signature, string message, WalletType walletType);
}