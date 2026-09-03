using FluentValidation;
using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("API Key name is required.")
            .MaximumLength(128).WithMessage("API Key name must not exceed 128 characters.");

        RuleFor(x => x.Environment)
            .Must(env => string.Equals(env, "live", StringComparison.OrdinalIgnoreCase) || string.Equals(env, "test", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Environment must be either 'live' or 'test'.");

        RuleFor(x => x.Scopes)
            .Must(scopes => (int)scopes > 0)
            .WithMessage("At least one ApiKeyScope must be specified.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > DateTimeOffset.UtcNow)
            .WithMessage("ExpiresAt must be in the future.");
    }
}
