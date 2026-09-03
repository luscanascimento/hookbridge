using FluentValidation;
using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed class CreateEndpointValidator : AbstractValidator<CreateEndpointCommand>
{
    public CreateEndpointValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("ApplicationId is required.");

        RuleFor(x => x.TargetUrl)
            .NotEmpty().WithMessage("TargetUrl is required.")
            .MaximumLength(2048).WithMessage("TargetUrl must not exceed 2048 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("TargetUrl must be a valid HTTP or HTTPS absolute URL.");

        RuleFor(x => x.Description)
            .MaximumLength(512).WithMessage("Description must not exceed 512 characters.");

        RuleFor(x => x.RateLimitPerMinute)
            .InclusiveBetween(1, 10000).WithMessage("RateLimitPerMinute must be between 1 and 10,000.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 30).WithMessage("TimeoutSeconds must be between 1 and 30 seconds.");
    }
}

public sealed class UpdateEndpointValidator : AbstractValidator<UpdateEndpointCommand>
{
    public UpdateEndpointValidator()
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty().WithMessage("TargetUrl is required.")
            .MaximumLength(2048).WithMessage("TargetUrl must not exceed 2048 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("TargetUrl must be a valid HTTP or HTTPS absolute URL.");

        RuleFor(x => x.Description)
            .MaximumLength(512).WithMessage("Description must not exceed 512 characters.");

        RuleFor(x => x.RateLimitPerMinute)
            .InclusiveBetween(1, 10000).WithMessage("RateLimitPerMinute must be between 1 and 10,000.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 30).WithMessage("TimeoutSeconds must be between 1 and 30 seconds.");
    }
}

public sealed class UpdateEndpointStatusValidator : AbstractValidator<UpdateEndpointStatusCommand>
{
    public UpdateEndpointStatusValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid EndpointStatus is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(512).WithMessage("Reason must not exceed 512 characters.");
    }
}
