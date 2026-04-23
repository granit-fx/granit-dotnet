using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Identity.Federated.Keycloak.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.BackgroundJobs.Tests.Jobs;

/// <summary>
/// Metadata smoke tests — ensure the recurring-job contract declared on
/// <see cref="KeycloakClientRoleSyncJob"/> stays aligned with ADR-030 (cron cadence
/// and canonical job name). The sync service behaviour itself is covered by the
/// existing <c>KeycloakClientRoleSyncServiceTests</c> suite.
/// </summary>
public sealed class KeycloakClientRoleSyncJobTests
{
    [Fact]
    public void Job_declares_the_canonical_cron_schedule()
    {
        RecurringJobAttribute? attr = typeof(KeycloakClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.CronExpression.ShouldBe("*/15 * * * *");
    }

    [Fact]
    public void Job_declares_the_canonical_job_name()
    {
        RecurringJobAttribute? attr = typeof(KeycloakClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("keycloak-client-role-sync");
    }

    [Fact]
    public void Job_implements_IBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(KeycloakClientRoleSyncJob)).ShouldBeTrue();
    }
}
