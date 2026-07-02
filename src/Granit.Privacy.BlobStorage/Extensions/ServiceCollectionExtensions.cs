using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Granit.Privacy.BlobStorage.Internal;
using Granit.Privacy.BlobStorage.Streaming;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.BlobStorage.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Privacy.BlobStorage</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the privacy-export services that bridge the scatter-gather saga to BlobStorage:
    /// <list type="bullet">
    ///   <item><see cref="PrivacyFragmentUploader"/> — provider-side fragment publisher.</item>
    ///   <item><see cref="StagedFragmentBuilder"/> (via <see cref="IStagedFragmentBuilder"/>) —
    ///   provider helper that serialises DTOs, uploads to staging, and signs the integrity tag.</item>
    ///   <item><see cref="IExportHmacSigner"/> default impl (<see cref="EphemeralExportHmacSigner"/>) —
    ///   production hosts override with a Vault-backed signer.</item>
    ///   <item><see cref="IPrivacyExportAssemblyService"/> (via <see cref="PrivacyExportAssemblyService"/>) —
    ///   sharded ZIP assembly driven by the background-job handler in
    ///   <c>Granit.Privacy.BackgroundJobs</c>.</item>
    ///   <item><see cref="IBlobBackedExportSource"/> (via <see cref="BlobBackedExportSource"/>) —
    ///   helper that yields HMAC-signed pass-through fragments for blob-backed providers
    ///   (Documents, attachments) without staging round-trips.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddGranitPrivacyBlobStorage(this IServiceCollection services)
    {
        // Explicit 30s timeout: the default 100s lets a slow blob-staging PUT tie up
        // upload slots through provider degradation. The assembly service uses its own
        // longer-running window for the manifest fetch path.
        services.AddHttpClient(StagedFragmentBuilder.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(PrivacyExportAssemblyService.HttpClientName);
        services.TryAddSingleton<EphemeralExportHmacSigner>();
        services.TryAddSingleton<IExportHmacSigner>(sp => sp.GetRequiredService<EphemeralExportHmacSigner>());
        services.TryAddSingleton<IExportContentSigner>(sp => sp.GetRequiredService<EphemeralExportHmacSigner>());

        // Application-layer confidentiality for the assembled SAR package (GDPR Art. 32):
        // the manifest is AES-256-GCM encrypted before upload and decrypted server-side on
        // the download path. The default key is ephemeral/in-process; production hosts
        // override IExportContentEncryptor with a Vault-backed impl (same story as the
        // HMAC signer above). The startup guard below fails closed outside Development.
        services.TryAddSingleton<EphemeralExportContentEncryptor>();
        services.TryAddSingleton<IExportContentEncryptor>(sp => sp.GetRequiredService<EphemeralExportContentEncryptor>());
        services.TryAddSingleton<IExportAssemblyCheckpointStore, InMemoryExportAssemblyCheckpointStore>();
        services.TryAddScoped<IStagedFragmentBuilder, StagedFragmentBuilder>();
        services.TryAddScoped<IBlobBackedExportSource, BlobBackedExportSource>();
        services.TryAddScoped<IPrivacyExportAssemblyService, PrivacyExportAssemblyService>();
        services.TryAddScoped<IPrivacyExportDownloadResolver, BlobBackedPrivacyExportDownloadResolver>();
        services.TryAddScoped<PrivacyFragmentUploader>();

        // Fail-fast guard: the in-memory ephemeral signer breaks shard verification
        // on any multi-replica or restart scenario. Hosts running outside Development
        // MUST register a shared-state signer (Vault-backed) BEFORE the host starts.
        services.AddHostedService<EphemeralExportHmacSignerStartupGuard>();

        // Fail-fast guard: the in-memory ephemeral encryptor's key is per-process, so an
        // encrypted SAR package cannot be decrypted after a restart or by another replica —
        // and shipping personal-data exports with a throwaway key fails GDPR Art. 32. Hosts
        // outside Development MUST register a shared-state (Vault-backed) IExportContentEncryptor.
        services.AddHostedService<EphemeralExportContentEncryptorStartupGuard>();
        return services;
    }
}
