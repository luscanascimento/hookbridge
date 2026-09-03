using System.Text.RegularExpressions;
using FluentValidation;
using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed partial class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    // Allows event patterns like "*", "order.*", "payment.settled.v1", "invoice.created"
    [GeneratedRegex(@"^(\*|[a-z0-9_\-]+(\.[a-z0-9_\-]+)*(\.\*)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EventPatternRegex();

    public CreateSubscriptionValidator()
    {
        RuleFor(x => x.EventTypePattern)
            .NotEmpty().WithMessage("EventTypePattern cannot be empty.")
            .MaximumLength(128).WithMessage("EventTypePattern must not exceed 128 characters.")
            .Must(pattern => !string.IsNullOrWhiteSpace(pattern) && EventPatternRegex().IsMatch(pattern.Trim()))
            .WithMessage("EventTypePattern must be a wildcard '*' or hierarchical pattern (e.g. 'order.*', 'payment.settled.v1').");
    }
}
