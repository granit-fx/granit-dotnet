using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging;

namespace Granit.OpenIddict.BackgroundJobs.Services;

/// <summary>
/// Executes the signing key rotation lifecycle via <see cref="IKeyRotationService"/>.
/// </summary>
public sealed partial class KeyRotationExecutionService(
    IKeyRotationService keyRotationService,
    OpenIddictMetrics metrics,
    ILogger<KeyRotationExecutionService> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        KeyRotationResult result = await keyRotationService
            .RotateAsync(cancellationToken).ConfigureAwait(false);

        // Signing keys are global (not tenant-scoped) — record under the "global" tenant.
        metrics.RecordKeyRotation(
            tenantId: null, result.KeysGenerated, result.KeysRetired, result.KeysRevoked);

        Log.KeyRotationCompleted(logger,
            result.KeysGenerated, result.KeysRetired, result.KeysRevoked, result.KeysPruned);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Key rotation completed: {Generated} generated, {Retired} retired, {Revoked} revoked, {Pruned} pruned.")]
        public static partial void KeyRotationCompleted(
            ILogger logger, int generated, int retired, int revoked, int pruned);
    }
}
