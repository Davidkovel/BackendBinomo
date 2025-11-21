using System.Text;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using Chaos.NaCl;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;

namespace BinomoBackend.Infrastructure.Services;

public class WalletSignatureValidator : IWalletSignatureValidator
{
    private readonly ILogger<WalletSignatureValidator> _logger;

    public WalletSignatureValidator(ILogger<WalletSignatureValidator> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ValidateSignatureAsync(
        string walletAddress, 
        string signature, 
        string message, 
        WalletType walletType)
    {
        try
        {
            return walletType switch
            {
                WalletType.MetaMask => await ValidateEthereumSignature(walletAddress, signature, message),
                WalletType.Phantom => await ValidateSolanaSignature(walletAddress, signature, message),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating wallet signature for {WalletAddress}", walletAddress);
            return false;
        }
    }

    private async Task<bool> ValidateEthereumSignature(string address, string signature, string message)
    {
        try
        {
            // Ethereum signature validation using Nethereum
            var signer = new EthereumMessageSigner();
            var messagePrefix = "\x19Ethereum Signed Message:\n" + message.Length;
            var fullMessage = messagePrefix + message;
            
            var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
            return string.Equals(recoveredAddress, address, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Ethereum signature");
            return false;
        }
    }

    private async Task<bool> ValidateSolanaSignature(string publicKey, string signature, string message)
    {
        try
        {
            // Solana signature validation
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var signatureBytes = Convert.FromBase64String(signature);
            var publicKeyBytes = Convert.FromBase64String(publicKey);

            // Using Ed25519 verification
            return Ed25519.Verify(signatureBytes, messageBytes, publicKeyBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Solana signature");
            return false;
        }
    }
}