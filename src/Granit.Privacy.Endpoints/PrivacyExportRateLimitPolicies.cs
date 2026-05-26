namespace Granit.Privacy.Endpoints;

/// <summary>
/// Well-known rate-limit policy names attached to privacy-export endpoints.
/// Hosts wire the policy bodies under <c>RateLimiting:Policies:{PolicyName}</c>
/// in <c>appsettings.json</c> — the framework only owns the policy <i>name</i>
/// so apps can pick the partition strategy and quotas that fit their tenancy
/// model (per-user vs per-tenant-and-IP, sliding-window vs token-bucket, etc.).
/// </summary>
/// <remarks>
/// <para>
/// <b>Default recommendation</b> for <see cref="ExportCreate"/>: sliding window,
/// partition by user, <c>PermitLimit = 1</c>, <c>Window = 24h</c>,
/// <c>SegmentsPerWindow = 24</c>. One personal-data export per subject per day
/// matches the typical user expectation and bounds the bucket / staging cost
/// of a hostile / buggy client.
/// </para>
/// <para>
/// Hosts that want softer or stricter quotas override the body in config —
/// the policy name is the contract, the body is the operational knob.
/// </para>
/// </remarks>
public static class PrivacyExportRateLimitPolicies
{
    /// <summary>
    /// Rate-limit policy guarding <c>POST /privacy/exports</c> — caps how often a
    /// data subject can spin up a fresh personal-data export.
    /// </summary>
    public const string ExportCreate = "privacy-export-create";
}
