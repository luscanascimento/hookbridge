namespace HookBridge.Domain.Enums;

[Flags]
public enum ApiKeyScope
{
    None = 0,
    EventsIngest = 1 << 0,     // 1
    DeliveriesRead = 1 << 1,   // 2
    DeliveriesReplay = 1 << 2, // 4
    EndpointsManage = 1 << 3,  // 8
    Admin = 1 << 4,            // 16
    All = EventsIngest | DeliveriesRead | DeliveriesReplay | EndpointsManage | Admin
}
