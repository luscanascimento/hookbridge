using System.Text.RegularExpressions;
using FluentValidation;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Auth.Validators;

public sealed partial class RegisterTenantValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantValidator()
    {
        RuleFor(x => x.TenantIdentifier)
            .NotEmpty().WithMessage("Tenant identifier is required.")
            .Length(3, 64).WithMessage("Tenant identifier must be between 3 and 64 characters.")
            .Matches(TenantSlugRegex()).WithMessage("Tenant identifier can only contain lowercase letters, numbers, hyphens, and underscores.");

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage("Tenant name is required.")
            .Length(2, 128).WithMessage("Tenant name must be between 2 and 128 characters.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("Admin email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[\!\@\#\$\%\^\&\*\(\)_\+\-\=\[\]\{\}\;\:\'\""\,\<\.\>\/\?]").WithMessage("Password must contain at least one special character.");
    }

    [GeneratedRegex("^[a-z0-9_\\-]+$")]
    private static partial Regex TenantSlugRegex();
}

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid role is required.")
            .Must(r => r != UserRole.SystemOperator).WithMessage("Cannot assign SystemOperator role via tenant invite.");

        RuleFor(x => x.InitialPassword)
            .NotEmpty().WithMessage("Initial password is required.")
            .MinimumLength(8).WithMessage("Initial password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
