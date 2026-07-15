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

        // First boot: generate the initial key set when the store is empty, so a deployment that
        // opted into rotation does not silently fall back to ephemeral keys.
        await EnsureInitializedAsync(stoppingToken).ConfigureAwait(false);

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
        InvalidateOptionsCache();
        Log.KeySetChanged(logger);
    }

    /// <summary>
    /// Generates the initial signing/encryption keys when rotation is enabled but the store is
    /// empty (fresh deployment) — without this the server would silently fall back to ephemeral
    /// keys despite the operator opting into rotation.
    /// </summary>
    /// <remarks>
    /// The pre-check narrows — but does not eliminate — the window in which two replicas booting
    /// against an empty store each generate a key; the surplus keys are all valid and are pruned by
    /// a later rotation cycle. A failed attempt (schema absent on first boot, transient DB error) is
    /// logged and retried at the next boot or by the rotation job — the server stays on ephemeral
    /// keys until then rather than crash-looping.
    /// </remarks>
    internal async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ISigningKeyStore store = scope.ServiceProvider.GetRequiredService<ISigningKeyStore>();
            if (await store.GetActiveKeyAsync("signing", cancellationToken).ConfigureAwait(false) is not null)
            {
                return;
            }

            IKeyRotationService rotation = scope.ServiceProvider.GetRequiredService<IKeyRotationService>();
            KeyRotationResult result = await rotation.RotateAsync(cancellationToken).ConfigureAwait(false);
            if (result.KeysGenerated > 0)
            {
                Log.FirstBootGenerated(logger, result.KeysGenerated);
                InvalidateOptionsCache();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // First-boot generation must survive a transient/absent store — logged, retried at next boot or by the rotation job.
        catch (Exception ex)
        {
            Log.FirstBootFailed(logger, ex);
        }
#pragma warning restore CA1031
    }

    // Evict the cached OpenIddictServerOptions so DatabaseSigningKeyPostConfigure re-runs and
    // reloads the credentials on the next resolve; rebuild eagerly so live requests observe the
    // new key set without waiting for the next options access.
    private void InvalidateOptionsCache()
    {
        optionsCache.TryRemove(Microsoft.Extensions.Options.Options.DefaultName);
        _ = optionsMonitor.Get(Microsoft.Extensions.Options.Options.DefaultName);
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

        [LoggerMessage(Level = LogLevel.Information,
            Message = "First boot: generated {Count} initial signing/encryption key(s) and loaded them.")]
        public static partial void FirstBootGenerated(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "First-boot key generation failed; staying on ephemeral keys until the next boot or rotation.")]
        public static partial void FirstBootFailed(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Failed to read the signing key set; will retry on the next refresh tick.")]
        public static partial void FingerprintFailed(ILogger logger, Exception exception);
    }
}
