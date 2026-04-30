using System.Collections.Concurrent;
using Granit.Authorization;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// Pure helper that mirrors the render-time permission gate
/// (<c>DashboardRenderer</c>'s 3.a step) on the push path: when a widget
/// declares a <see cref="WidgetPushMessage.RequiredPermission"/> the current
/// user lacks, the framework rewrites the envelope to
/// <see cref="WidgetSnapshotStatus.Unavailable"/> before forwarding to the
/// stream — never leaks a snapshot to a non-entitled subscriber, never leaks
/// a producer's specific reason key either.
/// </summary>
/// <remarks>
/// <para>
/// Lives separate from the SSE handler so it stays unit-testable without
/// spinning up a TestServer. The handler calls
/// <see cref="ResolveEffectiveAsync"/> on every inbound envelope and forwards
/// the result.
/// </para>
/// <para>
/// Per-permission lookups are cached for the lifetime of the stream connection
/// in the <paramref name="permissionCache"/> the caller owns — the same SSE
/// connection sees the same set of permissions throughout. Permission changes
/// mid-stream are intentionally not picked up; clients reconnect to refresh
/// their entitlements (matches the pull endpoint's snapshot-at-render-time
/// gate).
/// </para>
/// </remarks>
internal static class WidgetPushPermissionFilter
{
    /// <summary>
    /// Returns the envelope the subscriber actually receives — the original
    /// envelope when the user holds the required permission (or none is
    /// declared), an <c>Unavailable</c> envelope otherwise.
    /// </summary>
    public static async ValueTask<WidgetSnapshotEnvelope> ResolveEffectiveAsync(
        WidgetPushMessage message,
        IPermissionChecker permissionChecker,
        ConcurrentDictionary<string, bool> permissionCache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(permissionChecker);
        ArgumentNullException.ThrowIfNull(permissionCache);

        if (message.RequiredPermission is not { Length: > 0 } permission)
        {
            return message.Envelope;
        }

        if (!permissionCache.TryGetValue(permission, out bool granted))
        {
            granted = await permissionChecker
                .IsGrantedAsync(permission, cancellationToken)
                .ConfigureAwait(false);
            permissionCache[permission] = granted;
        }

        if (granted)
        {
            return message.Envelope;
        }

        // Replace with Unavailable, preserving sequence + emittedAt + refreshHint
        // so the wire shape stays identical to the render-time gating path. The
        // generic "Widget:Unavailable" reason key is intentional — never echoes
        // the producer's specific reason to a subscriber who isn't entitled to
        // see it.
        return WidgetSnapshotEnvelope.Unavailable(
            widgetType: message.Envelope.WidgetType,
            sequence: message.Envelope.Sequence,
            emittedAt: message.Envelope.EmittedAt,
            refreshHint: message.Envelope.RefreshHint);
    }
}
