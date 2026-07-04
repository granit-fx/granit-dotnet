using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Vault.Internal;

/// <summary>
/// FusionCache decorator for <see cref="ISecretStore"/>. Responsible for:
/// <list type="bullet">
///   <item>Serving recent reads from the L1 cache (TTL from <see cref="SecretStoreOptions.CacheSeconds"/>).</item>
///   <item>Emitting the observable <c>granit.vault.secret.read</c> counter — provider implementations
///         stay silent to avoid double-counting when a cache is in front of them.</item>
///   <item>Bypassing the cache for binaries above <see cref="SecretStoreOptions.MaxCachedBinarySizeBytes"/>
///         to protect the Large Object Heap.</item>
/// </list>
/// Registered by each provider's <c>AddGranitVault*</c> extension only when
/// <c>SecretStoreOptions.CacheSeconds &gt; 0</c>. Otherwise the concrete provider store is
/// bound directly as <see cref="ISecretStore"/> and the counter is emitted by the provider.
/// </summary>
/// <remarks>
/// Cache keys carry no explicit tenant prefix — Granit's <c>TenantAwareFusionCache</c>
/// decorator already prepends <c>t:{tenantId}:</c> to every key, so tenant isolation
/// is automatic. Key format: <c>vault:secret:{provider}:{name}:{version|latest}</c>.
/// </remarks>
internal sealed partial class CachedSecretStore(
    ISecretStore inner,
    IFusionCache cache,
    VaultMetrics metrics,
    IOptions<SecretStoreOptions> options,
    IServiceProvider serviceProvider,
    ILogger<CachedSecretStore> logger,
    string providerName) : ISecretStore
{
    private readonly SecretStoreOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string cacheKey = BuildCacheKey(request);
        string? tenantId = SecretStoreDiagnostics.ResolveTenantId(serviceProvider);

        MaybeValue<SecretDescriptor> cached = await cache
            .TryGetAsync<SecretDescriptor>(cacheKey, token: cancellationToken)
            .ConfigureAwait(false);

        if (cached.HasValue)
        {
            metrics.RecordSecretCacheHit(tenantId, providerName);
            metrics.RecordSecretRead(tenantId, providerName, outcome: "ok", cached: true);
            return cached.Value;
        }

        SecretDescriptor descriptor;
        try
        {
            descriptor = await inner.GetSecretAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (SecretVaultException ex)
        {
            SecretStoreDiagnostics.RecordAndThrow(metrics, tenantId, providerName, cached: false, ex);
            throw;
        }

        metrics.RecordSecretRead(tenantId, providerName, outcome: "ok", cached: false);

        if (ShouldCache(descriptor))
        {
            var entryOptions = new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromSeconds(_options.CacheSeconds),
            };
            await cache.SetAsync(cacheKey, descriptor, entryOptions, token: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogCacheBypassLargeBinary(logger, request.Name, descriptor.BinaryValue?.Length ?? 0);
        }

        return descriptor;
    }

    private bool ShouldCache(SecretDescriptor descriptor)
    {
        return !(descriptor.BinaryValue is { } binary) || binary.Length <= _options.MaxCachedBinarySizeBytes;
    }

    private string BuildCacheKey(SecretRequest request) =>
        $"vault:secret:{providerName}:{request.Name}:{request.Version?.Identifier ?? "latest"}";

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Secret '{SecretName}' is {SizeBytes} bytes — bypassing FusionCache (threshold: MaxCachedBinarySizeBytes).")]
    private static partial void LogCacheBypassLargeBinary(ILogger logger, string secretName, int sizeBytes);
}
