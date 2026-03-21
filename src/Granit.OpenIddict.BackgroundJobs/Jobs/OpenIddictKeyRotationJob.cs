using Granit.BackgroundJobs;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that handles automatic signing key rotation.
/// </summary>
/// <remarks>
/// <para>Runs daily. The handler:</para>
/// <list type="number">
/// <item>Generates a new key if the active key expires within <c>RotationLeadTime</c></item>
/// <item>Retires the old key (keeps it for verification during <c>GracePeriod</c>)</item>
/// <item>Revokes keys whose grace period has expired</item>
/// <item>Prunes revoked keys older than 30 days</item>
/// </list>
/// </remarks>
[RecurringJob("0 3 * * *", "openiddict-key-rotation")]
public sealed record OpenIddictKeyRotationJob : IBackgroundJob;
