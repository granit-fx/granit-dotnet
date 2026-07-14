using Granit.BackgroundJobs;
using Granit.DataExchange.BackgroundJobs.Options;
using Granit.DataExchange.BackgroundJobs.Services;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.BackgroundJobs;

/// <summary>
/// Granit module that registers the GDPR retention sweep background job for
/// <c>Granit.DataExchange</c>: purges expired import/export files and job records, and recovers
/// jobs stranded in a non-terminal executing state.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<DataExchangeRetentionOptions>()
            .BindConfiguration(DataExchangeRetentionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddSingleton<IValidateOptions<DataExchangeRetentionOptions>, DataExchangeRetentionOptionsValidator>();
        context.Services.TryAddTransient<RetentionSweepService>();
    }
}
