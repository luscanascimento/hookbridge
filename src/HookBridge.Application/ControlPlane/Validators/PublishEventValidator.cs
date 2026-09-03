using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Integration.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed class PublishEventValidator : AbstractValidator<PublishEventCommand>
{
    public PublishEventValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("EventType is required.")
            .MaximumLength(128).WithMessage("EventType must not exceed 128 characters.");

        RuleFor(x => x.Payload)
            .Must(p => p.ValueKind != JsonValueKind.Undefined && p.ValueKind != JsonValueKind.Null)
            .WithMessage("Payload must be a valid JSON object or value.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128).WithMessage("IdempotencyKey must not exceed 128 characters.");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(128).WithMessage("CorrelationId must not exceed 128 characters.");
    }
}
