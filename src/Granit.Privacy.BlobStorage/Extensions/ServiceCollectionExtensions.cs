using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.BlobStorage.DataExport.Internal;
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
        services.AddHttpClient(StagedFragmentBuilder.HttpClientName);
        services.AddHttpClient(PrivacyExportAssemblyService.HttpClientName);
        services.TryAddSingleton<EphemeralExportHmacSigner>();
        services.TryAddSingleton<IExportHmacSigner>(sp => sp.GetRequiredService<EphemeralExportHmacSigner>());
        services.TryAddSingleton<IExportContentSigner>(sp => sp.GetRequiredService<EphemeralExportHmacSigner>());
        services.TryAddSingleton<IExportAssemblyCheckpointStore, InMemoryExportAssemblyCheckpointStore>();
        services.TryAddScoped<IStagedFragmentBuilder, StagedFragmentBuilder>();
        services.TryAddScoped<IBlobBackedExportSource, BlobBackedExportSource>();
        services.TryAddScoped<IPrivacyExportAssemblyService, PrivacyExportAssemblyService>();
        services.TryAddScoped<IPrivacyExportDownloadResolver, BlobBackedPrivacyExportDownloadResolver>();
        services.TryAddScoped<PrivacyFragmentUploader>();
        return services;
    }
}
