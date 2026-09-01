namespace HookBridge.Domain.Enums;

public enum DeliveryStatus
{
    Pending = 1,
    Dispatched = 2,
    Success = 3,
    Failed = 4,
    DeadLettered = 5,
    Cancelled = 6
}
