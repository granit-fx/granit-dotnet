using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.GoogleCloud.Diagnostics;
using Granit.BlobStorage.GoogleCloud.HealthChecks;
using Granit.BlobStorage.GoogleCloud.Internal;
using Granit.BlobStorage.GoogleCloud.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.GoogleCloud.Extensions;

/// <summary>
/// Extension methods for registering the Google Cloud Storage blob storage provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageGoogleCloudHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.GoogleCloud</c> services: GCS client, key strategy, and the
    /// <see cref="Granit.BlobStorage.IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="GoogleCloudStorageOptions"/> from the <c>"BlobStorage"</c> configuration section.
    /// Authentication uses Application Default Credentials (ADC) by default. For explicit
    /// service account key files, set <c>CredentialFilePath</c>.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageGoogleCloud(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(BlobStorageGoogleCloudActivitySource.Name);

        builder.Services
            .AddOptions<GoogleCloudStorageOptions>()
            .BindConfiguration(BlobStorageOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<GoogleCloudStorageOptions>, GoogleCloudStorageOptionsValidator>();

        // GoogleCloudBlobClient implements IBlobStoreProvider + IPresignedUrlProvider.
        // Registered as Singleton: StorageClient is thread-safe and intended for reuse.
        builder.Services.AddSingleton<GoogleCloudBlobClient>();
        builder.Services.AddSingleton<IBlobStoreProvider>(sp => sp.GetRequiredService<GoogleCloudBlobClient>());
        builder.Services.AddSingleton<IPresignedUrlProvider>(sp => sp.GetRequiredService<GoogleCloudBlobClient>());

        builder.Services.AddScoped<IBlobKeyStrategy, GoogleCloudBlobKeyStrategy>();
        builder.Services.AddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }

    /// <summary>
    /// Adds a GCS connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Verifies that the default bucket is accessible by issuing a <c>ListObjects</c> request.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"gcs"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitGoogleCloudStorageHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "gcs",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<GoogleCloudStorageHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<GoogleCloudStorageHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
