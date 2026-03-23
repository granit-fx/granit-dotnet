using Granit.Timeline.Endpoints.Permissions;

namespace Granit.Timeline.Endpoints.Internal;

/// <summary>
/// Authorization policy constants for timeline endpoints.
/// </summary>
internal static class TimelineAuthorizationPolicy
{
    /// <summary>
    /// Name of the authorization policy that guards all timeline endpoints.
    /// Equals <see cref="TimelinePermissions.Entries.Read"/> (<c>"Timeline.Entries.Read"</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>GranitAuthorizationModule</c> is loaded, <c>DynamicPermissionPolicyProvider</c>
    /// resolves this policy via <c>PermissionRequirement</c> — the full <c>IPermissionChecker</c>
    /// pipeline applies (AdminRole bypass, cache, <c>IPermissionGrantStore</c>).
    /// </para>
    /// <para>
    /// Without <c>GranitAuthorizationModule</c> (e.g. unit tests), the policy falls back to
    /// the role-based check registered by
    /// <see cref="Extensions.TimelineEndpointRouteBuilderExtensions.MapTimelineEndpoints"/>.
    /// </para>
    /// </remarks>
    public const string PolicyName = TimelinePermissions.Entries.Read;
}
