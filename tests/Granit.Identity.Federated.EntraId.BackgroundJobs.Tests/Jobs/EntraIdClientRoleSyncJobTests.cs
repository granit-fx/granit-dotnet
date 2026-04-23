using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Identity.Federated.EntraId.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.BackgroundJobs.Tests.Jobs;

/// <summary>
/// Metadata smoke tests — ensure the recurring-job contract declared on
/// <see cref="EntraIdClientRoleSyncJob"/> stays aligned with ADR-030. The sync
/// service behaviour itself is covered by <c>EntraIdClientRoleSyncServiceTests</c>.
/// </summary>
public sealed class EntraIdClientRoleSyncJobTests
{
    [Fact]
    public void Job_declares_the_canonical_cron_schedule()
    {
        RecurringJobAttribute? attr = typeof(EntraIdClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.CronExpression.ShouldBe("*/15 * * * *");
    }

    [Fact]
    public void Job_declares_the_canonical_job_name()
    {
        RecurringJobAttribute? attr = typeof(EntraIdClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("entraid-client-role-sync");
    }

    [Fact]
    public void Job_implements_IBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(EntraIdClientRoleSyncJob)).ShouldBeTrue();
    }
}
