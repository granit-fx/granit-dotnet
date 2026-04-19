using Granit.Privacy.BlobStorage.DataExport;
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
    ///   <item><see cref="PrivacyFragmentUploader"/> — provider-side fragment upload helper.</item>
    ///   <item><see cref="ExportArchiveAssemblyHandler"/> — terminal ZIP assembler triggered by
    ///   <see cref="Granit.Privacy.DataExport.Events.ExportCompletedEto"/>. Auto-discovered by
    ///   Wolverine once registered with DI.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddGranitPrivacyBlobStorage(this IServiceCollection services)
    {
        services.AddHttpClient(PrivacyFragmentUploader.HttpClientName);
        services.AddHttpClient(ExportArchiveAssemblyHandler.HttpClientName);
        services.TryAddScoped<PrivacyFragmentUploader>();
        services.TryAddScoped<ExportArchiveAssemblyHandler>();
        return services;
    }
}
