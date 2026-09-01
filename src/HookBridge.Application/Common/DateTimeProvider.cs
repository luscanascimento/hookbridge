using HookBridge.Application.Abstractions;

namespace HookBridge.Application.Common;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
