using Granit.Caching.Options;
using Granit.Caching.Vault.Options;
using Granit.Vault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Vault.Internal;

/// <summary>
/// Hydrates <see cref="CacheEncryptionOptions.Key"/> from <see cref="ISecretStore"/>
/// at first resolution of <see cref="IOptions{TOptions}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Fail-closed semantics:
/// </para>
/// <list type="bullet">
///   <item>Throws if <see cref="CachingOptions.EncryptValues"/> is <c>false</c> — bridge wired but encryption disabled is a configuration error.</item>
///   <item>Throws if no <see cref="ISecretStore"/> is registered — typical when Vault providers auto-disable in Development; never silently falls back to plaintext.</item>
///   <item>Throws if the Vault call fails (transient, access denied, not found) — surfaces the underlying <see cref="Granit.Vault.Exceptions.SecretVaultException"/>.</item>
/// </list>
/// <para>
/// Sync-blocks on <see cref="Granit.Vault.ISecretStore.GetSecretAsync"/> via
/// <c>GetAwaiter().GetResult()</c>. Acceptable because post-configure runs once at host
/// startup (forced eagerly by <c>ValidateOnStart</c> on <see cref="CacheEncryptionOptions"/>),
/// not on the hot path. Same pattern as
/// <c>Granit.OpenIddict.Internal.DatabaseSigningKeyPostConfigure</c>.
/// </para>
/// </remarks>
internal sealed partial class VaultCacheEncryptionPostConfigure(
    IServiceScopeFactory scopeFactory,
    IOptions<CacheEncryptionVaultOptions> vaultOptions,
    IOptions<CachingOptions> cachingOptions,
    ILogger<VaultCacheEncryptionPostConfigure> logger)
    : IPostConfigureOptions<CacheEncryptionOptions>
{
    public void PostConfigure(string? name, CacheEncryptionOptions options)
    {
        string? secretName = vaultOptions.Value.SecretName;
        if (string.IsNullOrWhiteSpace(secretName))
        {
            // Module is config-gated; this branch is defensive and should be unreachable.
            return;
        }

        if (!cachingOptions.Value.EncryptValues)
        {
            throw new InvalidOperationException(
                "Cache:Encryption:Vault:SecretName is configured but Cache:EncryptValues=false. " +
                "Remove the Vault bridge configuration or set Cache:EncryptValues=true.");
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        ISecretStore? secretStore = scope.ServiceProvider.GetService<ISecretStore>();
        if (secretStore is null)
        {
            throw new InvalidOperationException(
                $"Cache:Encryption:Vault:SecretName='{secretName}' requires an ISecretStore implementation, " +
                "but none is registered. Install a Vault provider package (Granit.Vault.HashiCorp, " +
                ".Azure, .Aws, or .GoogleCloud). In Development, providers auto-disable — either run a " +
                "local Vault, or clear Cache:Encryption:Vault:SecretName and set a static " +
                "Cache:Encryption:Key in appsettings.Development.json instead.");
        }

        SecretRequest request = string.IsNullOrWhiteSpace(vaultOptions.Value.SecretVersion)
            ? SecretRequest.Latest(secretName)
            : SecretRequest.At(secretName, vaultOptions.Value.SecretVersion);

        SecretDescriptor descriptor = secretStore
            .GetSecretAsync(request)
            .GetAwaiter()
            .GetResult();

        options.Key = descriptor.AsString();
        Log.KeyHydrated(logger, secretName, descriptor.Version ?? "<latest>");
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Cache encryption key hydrated from Vault (secret: '{SecretName}', version: '{Version}').")]
        public static partial void KeyHydrated(ILogger logger, string secretName, string version);
    }
}
