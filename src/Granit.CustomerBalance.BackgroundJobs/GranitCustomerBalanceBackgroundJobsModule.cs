using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.CustomerBalance.BackgroundJobs;

/// <summary>
/// Background jobs for customer balance: periodic scan for expired promotional credits.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitCustomerBalanceModule))]
public sealed class GranitCustomerBalanceBackgroundJobsModule : GranitModule;
