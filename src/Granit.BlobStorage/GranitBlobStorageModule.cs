using Granit.BlobStorage.Diagnostics;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Exports;
using Granit.BlobStorage.Queries;
using Granit.BlobStorage.Validators;
using Granit.DataExchange.Extensions;
using Granit.Guids;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
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
/// Add the optional <c>Granit.BlobStorage.Analytics</c> + <c>Granit.BlobStorage.Dashboards</c>
/// satellites to surface storage metrics and the operations dashboard.
/// <para>
/// Localization resources (<c>Localization/BlobStorage/{culture}.json</c>) are embedded in this
/// assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="BlobStorageLocalizationResource"/>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitGuidsModule))]
public sealed class GranitBlobStorageModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<BlobStorageMetrics>();

        context.Services.AddSingleton<IBlobValidator, ContentTypeAllowlistValidator>();
        context.Services.AddSingleton<IBlobValidator, MagicBytesValidator>();
        context.Services.AddSingleton<IBlobValidator, MaxSizeValidator>();

        context.Services.AddQueryDefinition<BlobDescriptor, BlobDescriptorQueryDefinition>();
        context.Services.AddExportDefinition<BlobDescriptor, BlobDescriptorExportDefinition>();
    }
}
