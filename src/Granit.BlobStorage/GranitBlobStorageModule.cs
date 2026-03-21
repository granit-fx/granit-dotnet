using Granit.BackgroundJobs;
using Granit.BlobStorage.Diagnostics;
using Granit.Core.Modularity;
using Granit.Guids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.BlobStorage;

/// <summary>
/// Granit module for blob storage (provider-agnostic core).
/// </summary>
/// <remarks>
/// Defines the <see cref="IBlobStorage"/>, <see cref="IBlobDescriptorStore"/>,
/// <see cref="IBlobKeyStrategy"/>, and <see cref="IBlobValidator"/> abstractions.
/// Register a concrete provider (e.g. <c>Granit.BlobStorage.S3</c>) and a
/// persistence adapter (e.g. <c>Granit.BlobStorage.EntityFrameworkCore</c>) alongside this module.
/// <para>
/// Localization resources (<c>Localization/BlobStorage/{culture}.json</c>) are embedded in this
/// assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="BlobStorageLocalizationResource"/>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitGuidsModule))]
public sealed class GranitBlobStorageModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddSingleton<BlobStorageMetrics>();
}
