using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictKeyRotationJob"/>.
/// Delegates all rotation logic to <see cref="IKeyRotationService"/>.
/// </summary>
internal static partial class OpenIddictKeyRotationHandler
{
    /// <summary>
    /// Executes the key rotation lifecycle via <see cref="IKeyRotationService"/>.
    /// </summary>
    public static async Task HandleAsync(
        OpenIddictKeyRotationJob _,
        IKeyRotationService keyRotationService,
        ILogger<OpenIddictKeyRotationJob> logger,
        CancellationToken cancellationToken)
    {
        KeyRotationResult result = await keyRotationService
            .RotateAsync(cancellationToken).ConfigureAwait(false);

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
