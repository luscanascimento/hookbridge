using System.Diagnostics.CodeAnalysis;

namespace HookBridge.Domain.Common;

/// <summary>
/// Represents the outcome of an operation without a return payload.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && error != DomainError.None)
        {
            throw new InvalidOperationException("A successful result cannot contain an error.");
        }

        if (!isSuccess && error == DomainError.None)
        {
            throw new InvalidOperationException("A failing result must specify a domain error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);

    public static Result Failure(DomainError error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<TValue>(value, true, DomainError.None);
    }

    public static Result<TValue> Failure<TValue>(DomainError error) =>
        new(default, false, error);
}

/// <summary>
/// Represents the outcome of an operation containing a return payload <typeparamref name="TValue"/>.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, DomainError error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(DomainError error) => Failure<TValue>(error);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<DomainError, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error);
}
