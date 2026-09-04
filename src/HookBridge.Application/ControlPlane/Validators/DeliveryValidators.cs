using FluentValidation;
using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed class ReplayDeliveryValidator : AbstractValidator<ReplayDeliveryCommand>
{
    public ReplayDeliveryValidator()
    {
        RuleFor(x => x.OverrideEndpointId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("OverrideEndpointId, when specified, must not be empty.");
    }
}

public sealed class BulkReplayDeliveriesValidator : AbstractValidator<BulkReplayDeliveriesCommand>
{
    public BulkReplayDeliveriesValidator()
    {
        RuleFor(x => x.MaxCount)
            .InclusiveBetween(1, 500)
            .When(x => x.MaxCount.HasValue)
            .WithMessage("MaxCount must be between 1 and 500.");

        RuleFor(x => x.DeliveryIds)
            .Must(ids => ids == null || ids.Count <= 500)
            .WithMessage("DeliveryIds list cannot exceed 500 items.");

        RuleFor(x => x.EventType)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.EventType))
            .WithMessage("EventType must not exceed 128 characters.");
    }
}
