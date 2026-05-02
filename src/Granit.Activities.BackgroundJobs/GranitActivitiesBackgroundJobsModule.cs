using Granit.Activities.BackgroundJobs.Services;
using Granit.BackgroundJobs;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Activities.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Activities — the every-5-min
/// overdue scan and the daily reminder scan. Recurring jobs auto-discovered by
/// <c>Granit.BackgroundJobs</c> via the <see cref="RecurringJobAttribute"/> on
/// each <see cref="IBackgroundJob"/> record.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitActivitiesModule))]
public sealed class GranitActivitiesBackgroundJobsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<MarkOverdueScanService>();
        context.Services.TryAddTransient<SendRemindersScanService>();
    }
}
