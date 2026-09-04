using FluentAssertions;
using HookBridge.Domain.Diagnostics;

namespace HookBridge.UnitTests.Diagnostics;

public class HookBridgeDiagnosticsTests
{
    [Fact]
    public void Diagnostics_ShouldProvideValidSourceAndMeter()
    {
        // Assert
        HookBridgeDiagnostics.ServiceName.Should().Be("HookBridge");
        HookBridgeDiagnostics.ActivitySource.Name.Should().Be("HookBridge.ControlPlane");
        HookBridgeDiagnostics.Meter.Name.Should().Be("HookBridge.ControlPlane");

        HookBridgeDiagnostics.DeliveriesDispatched.Should().NotBeNull();
        HookBridgeDiagnostics.DeliveriesSucceeded.Should().NotBeNull();
        HookBridgeDiagnostics.DeliveriesFailed.Should().NotBeNull();
        HookBridgeDiagnostics.DeliveryLatency.Should().NotBeNull();
        HookBridgeDiagnostics.ActiveSignalRConnections.Should().NotBeNull();
        HookBridgeDiagnostics.RealtimeEventsBroadcasted.Should().NotBeNull();
    }
}
