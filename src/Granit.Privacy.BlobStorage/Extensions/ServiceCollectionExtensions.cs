using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.BlobStorage.Streaming;
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
    ///   <item><see cref="ExportArchiveAssemblyHandler"/> — terminal ZIP assembler triggered by
    ///   <see cref="Granit.Privacy.DataExport.Events.ExportCompletedEto"/>. Auto-discovered by
    ///   Wolverine once registered with DI.</item>
    ///   <item><see cref="IBlobBackedExportSource"/> (via <see cref="BlobBackedExportSource"/>) —
    ///   helper that yields HMAC-signed pass-through fragments for blob-backed providers
    ///   (Documents, attachments) without staging round-trips.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddGranitPrivacyBlobStorage(this IServiceCollection services)
    {
        services.AddHttpClient(StagedFragmentBuilder.HttpClientName);
        services.AddHttpClient(ExportArchiveAssemblyHandler.HttpClientName);
        services.TryAddSingleton<IExportHmacSigner, EphemeralExportHmacSigner>();
        services.TryAddScoped<IStagedFragmentBuilder, StagedFragmentBuilder>();
        services.TryAddScoped<IBlobBackedExportSource, BlobBackedExportSource>();
        services.TryAddScoped<PrivacyFragmentUploader>();
        services.TryAddScoped<ExportArchiveAssemblyHandler>();
        return services;
    }
}
