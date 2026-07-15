using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// Keeps each running instance's OpenIddict signing/encryption credentials in sync with the
/// database when database-backed key rotation is enabled.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DatabaseSigningKeyPostConfigure"/> loads the credentials once, at the first resolve
/// of <see cref="OpenIddictServerOptions"/>. That snapshot never changes afterwards, so after the
/// rotation job mints a new key on one node the other running replicas keep signing with — and
/// exposing at <c>/.well-known/jwks</c> — the old key set until they restart. Tokens signed by the
/// rotated-in key then fail validation on the stale replicas.
/// </para>
/// <para>
/// This service polls the key store every <see cref="GranitKeyRotationOptions.RefreshCheckInterval"/>
/// and, when the active/retired key set changes, evicts the cached options so the post-configure
/// re-runs and reloads the current credentials. Each replica polls independently — no distributed
/// coordination is required because the store is the single source of truth.
/// </para>
/// </remarks>
internal sealed partial class SigningKeyRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitorCache<OpenIddictServerOptions> optionsCache,
    IOptionsMonitor<OpenIddictServerOptions> optionsMonitor,
    IOptions<GranitKeyRotationOptions> rotationOptions,
    TimeProvider timeProvider,
    ILogger<SigningKeyRefreshService> logger) : BackgroundService
{
    private string? _lastFingerprint;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rotationOptions.Value.Enabled)
        {
            // Ephemeral/static keys — nothing to refresh.
            return;
        }

        // Prime from the current key set so the first tick reacts only to a subsequent change,
        // not to the state the post-configure already loaded at startup.
        await PrimeAsync(stoppingToken).ConfigureAwait(false);

        using PeriodicTimer timer = new(rotationOptions.Value.RefreshCheckInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RefreshIfChangedAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Captures the current key-set fingerprint without triggering a reload.</summary>
    internal async Task PrimeAsync(CancellationToken cancellationToken) =>
        _lastFingerprint = await SafeComputeFingerprintAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reloads the credentials when the key-set fingerprint has changed since the last check.
    /// A failed read (schema absent on first boot, transient DB error) is a no-op — the loop
    /// simply retries on the next tick.
    /// </summary>
    internal async Task RefreshIfChangedAsync(CancellationToken cancellationToken)
    {
        string? current = await SafeComputeFingerprintAsync(cancellationToken).ConfigureAwait(false);
        if (current is null || string.Equals(current, _lastFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastFingerprint = current;

        // Evict the cached OpenIddictServerOptions so DatabaseSigningKeyPostConfigure re-runs and
        // reloads the active/retired credentials on the next resolve; rebuild eagerly so live
        // requests observe the new key set without waiting for the next options access.
        optionsCache.TryRemove(Microsoft.Extensions.Options.Options.DefaultName);
        _ = optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName);
        Log.KeySetChanged(logger);
    }

    private async Task<string?> SafeComputeFingerprintAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ISigningKeyStore store = scope.ServiceProvider.GetRequiredService<ISigningKeyStore>();
            IReadOnlyList<SigningKey> keys = await store
                .GetKeysAsync([SigningKeyStatus.Active, SigningKeyStatus.Retired], cancellationToken)
                .ConfigureAwait(false);

            return string.Join('|', keys
                .Select(k => $"{k.KeyId}:{k.Status}")
                .OrderBy(s => s, StringComparer.Ordinal));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A background poll must survive transient store failures (incl. schema-absent on first boot) — logged and retried next tick.
        catch (Exception ex)
        {
            Log.FingerprintFailed(logger, ex);
            return null;
        }
#pragma warning restore CA1031
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Signing key set changed — reloaded OpenIddict credentials from the database.")]
        public static partial void KeySetChanged(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Failed to read the signing key set; will retry on the next refresh tick.")]
        public static partial void FingerprintFailed(ILogger logger, Exception exception);
    }
}
