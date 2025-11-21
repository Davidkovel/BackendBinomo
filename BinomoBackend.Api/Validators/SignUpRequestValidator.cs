using BinomoBackend.Application.DTOs.Auth;
using FluentValidation;

namespace BinomoBackend.Api.Validators;

public class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Valid email is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("Password must contain at least one number")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one special character");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}

public class WalletAuthRequestValidator : AbstractValidator<WalletAuthRequest>
{
    public WalletAuthRequestValidator()
    {
        RuleFor(x => x.WalletAddress)
            .NotEmpty()
            .WithMessage("Wallet address is required");

        RuleFor(x => x.Signature)
            .NotEmpty()
            .WithMessage("Signature is required");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Valid email is required");

        RuleFor(x => x.WalletType)
            .IsInEnum()
            .WithMessage("Invalid wallet type");
    }
}