using Granit.DataExchange.BlobStorage.Internal;
using Granit.DataExchange.BlobStorage.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DataExchange.BlobStorage.Extensions;

/// <summary>
/// Extension methods for registering the blob-storage-backed <see cref="IDataExchangeFileProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BlobStorageFileProvider"/> as the <see cref="IDataExchangeFileProvider"/>
    /// implementation, replacing the default in-memory fallback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataExchangeBlobStorage(this IServiceCollection services)
    {
        services.AddOptions<DataExchangeBlobStorageOptions>()
            .BindConfiguration(DataExchangeBlobStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Replace(ServiceDescriptor.Scoped<IDataExchangeFileProvider, BlobStorageFileProvider>());

        return services;
    }
}
