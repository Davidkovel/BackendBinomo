using BinomoBackend.Application.DTOs.Trading;
using BinomoBackend.Domain.Enums;
using FluentValidation;

namespace BinomoBackend.Api.Validators;


public class OpenPositionRequestValidator : AbstractValidator<OpenPositionRequest>
{
    public OpenPositionRequestValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required")
            .Matches(@"^[A-Z]+$")
            .WithMessage("Symbol must contain only uppercase letters");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid position type");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Amount must be less than or equal to 1,000,000");

        RuleFor(x => x.Leverage)
            .InclusiveBetween(1, 125)
            .WithMessage("Leverage must be between 1 and 125");

        RuleFor(x => x.OrderType)
            .IsInEnum()
            .WithMessage("Invalid order type");

        RuleFor(x => x.LimitPrice)
            .GreaterThan(0)
            .When(x => x.OrderType == OrderType.Limit)
            .WithMessage("Limit price must be greater than 0 for limit orders");

        RuleFor(x => x.StopLoss)
            .GreaterThan(0)
            .When(x => x.StopLoss.HasValue)
            .WithMessage("Stop loss must be greater than 0");

        RuleFor(x => x.TakeProfit)
            .GreaterThan(0)
            .When(x => x.TakeProfit.HasValue)
            .WithMessage("Take profit must be greater than 0");
    }
}