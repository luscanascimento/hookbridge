using FluentValidation;
using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Application.ControlPlane.Validators;

public sealed class CreateApplicationValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Application name is required.")
            .MaximumLength(128).WithMessage("Application name must not exceed 128 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(512).WithMessage("Description must not exceed 512 characters.");
    }
}

public sealed class UpdateApplicationValidator : AbstractValidator<UpdateApplicationCommand>
{
    public UpdateApplicationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Application name is required.")
            .MaximumLength(128).WithMessage("Application name must not exceed 128 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(512).WithMessage("Description must not exceed 512 characters.");
    }
}
