using System.Diagnostics;

namespace Granit.Presence.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Presence distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class PresenceActivitySource
{
    /// <summary>The name of the Granit.Presence <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Presence";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string RecordPoll = "presence.record_poll";
    internal const string QueryEffective = "presence.query_effective";
    internal const string SetOverride = "presence.set_override";
    internal const string GateNotification = "presence.gate_notification";

    // ──── Resource-room operations ────

    internal const string RoomJoin = "Granit.Presence.Room.Join";
    internal const string RoomLeave = "Granit.Presence.Room.Leave";
    internal const string RoomGet = "Granit.Presence.Room.Get";
}
