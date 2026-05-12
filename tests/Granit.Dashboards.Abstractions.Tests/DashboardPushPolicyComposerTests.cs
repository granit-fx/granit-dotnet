using Granit.Dashboards;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Pins the composition matrix from ADR-043 §2.3. The same function feeds the
/// render-time projector and the future push hub — every widget in the system
/// answers a single boolean question through this helper, so the matrix MUST
/// stay byte-stable. A behavior change here is a wire break.
/// </summary>
public sealed class DashboardPushPolicyComposerTests
{
    [Theory]
    // PullOnly — every hint pulls, no push channel ever.
    [InlineData(DashboardPushPolicy.PullOnly, RefreshHint.Static, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.PullOnly, RefreshHint.Dynamic, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.PullOnly, RefreshHint.Realtime, WidgetTransport.Pull)]
    // WhenWidgetsRequest — only Realtime widgets push.
    [InlineData(DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Static, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Dynamic, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Realtime, WidgetTransport.Push)]
    // Force — Dynamic / Realtime push; Static stays pull (no value pushing a Markdown banner).
    [InlineData(DashboardPushPolicy.Force, RefreshHint.Static, WidgetTransport.Pull)]
    [InlineData(DashboardPushPolicy.Force, RefreshHint.Dynamic, WidgetTransport.Push)]
    [InlineData(DashboardPushPolicy.Force, RefreshHint.Realtime, WidgetTransport.Push)]
    public void Compose_PinsMatrix(DashboardPushPolicy policy, RefreshHint hint, WidgetTransport expected)
    {
        DashboardPushPolicyComposer.Compose(policy, hint).ShouldBe(expected);
    }

    [Fact]
    public void Compose_DefaultEnumValues_ProducePullOnly()
    {
        // Defensive: the default value of every enum (0) is what NSubstitute returns
        // for unconfigured mocks, what new Dashboard descriptors produce before any
        // override, and what a freshly-defaulted DTO carries on the wire. Compose
        // MUST stay safe under those defaults.
        DashboardPushPolicyComposer.Compose(default, default).ShouldBe(WidgetTransport.Pull);
    }
}
