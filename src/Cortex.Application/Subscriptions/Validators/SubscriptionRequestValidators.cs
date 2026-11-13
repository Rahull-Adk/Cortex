using Cortex.Application.Subscriptions.Dtos;
using FluentValidation;

namespace Cortex.Application.Subscriptions.Validators;

public sealed class StartCheckoutRequestValidator : AbstractValidator<StartCheckoutRequest>
{
    public StartCheckoutRequestValidator() =>
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(50);
}
