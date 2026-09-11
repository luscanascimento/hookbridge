using FluentValidation.Results;
using HookBridge.Domain.Common;

namespace HookBridge.Application.Common;

public static class ValidationResultExtensions
{
    public static DomainError ToDomainError(this ValidationResult validation)
    {
        if (validation.IsValid)
        {
            return DomainError.None;
        }

        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var first = validation.Errors.FirstOrDefault();
        var code = first?.PropertyName ?? "Validation.Failed";
        var message = first?.ErrorMessage ?? "One or more validation errors occurred.";

        return DomainError.Validation(code, message, errors);
    }
}
