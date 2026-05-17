using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.Endpoints.Internal;
using Granit.BlobStorage.Endpoints.Options;
using Granit.Http.ApiDocumentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.BlobStorage.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering blob storage administration endpoint services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageEndpointsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.Endpoints</c> services: binds
    /// <see cref="BlobStorageEndpointsOptions"/> from the
    /// <c>"BlobStorageEndpoints"</c> configuration section and registers the
    /// OpenAPI schema example provider.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageEndpoints(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<BlobStorageEndpointsOptions>()
            .BindConfiguration(BlobStorageEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, BlobStorageSchemaExampleProvider>());

        return builder;
    }
}
