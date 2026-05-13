using Granit.Modularity;
using Granit.Observability;
using OpenTelemetry.Trace;

namespace Granit.BlobStorage.S3;

/// <summary>
/// Granit module for the S3-compatible blob storage provider.
/// </summary>
/// <remarks>
/// Registers <c>S3BlobClient</c> as both <c>IBlobStoreProvider</c> and
/// <c>IPresignedUrlProvider</c> when <see cref="Extensions.BlobStorageS3HostApplicationBuilderExtensions.AddGranitBlobStorageS3"/>
/// is called. Also contributes AWS SDK OTel tracing to <see cref="GranitOpenTelemetryRegistry"/>
/// so S3 calls appear as spans when <c>Granit.Observability</c> is hosted.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageS3Module : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        GranitOpenTelemetryRegistry.RegisterTracing(t => t.AddAWSInstrumentation());
}
