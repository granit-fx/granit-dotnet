using Granit.Privacy.BlobStorage.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.BlobStorage.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Privacy.BlobStorage</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PrivacyFragmentUploader"/> as a scoped service. Provider-side
    /// Wolverine handlers resolve it to upload their fragments and publish
    /// <see cref="Granit.Privacy.DataExport.Events.PersonalDataPreparedEto"/>.
    /// </summary>
    public static IServiceCollection AddGranitPrivacyBlobStorage(this IServiceCollection services)
    {
        services.AddHttpClient(PrivacyFragmentUploader.HttpClientName);
        services.TryAddScoped<PrivacyFragmentUploader>();
        return services;
    }
}
