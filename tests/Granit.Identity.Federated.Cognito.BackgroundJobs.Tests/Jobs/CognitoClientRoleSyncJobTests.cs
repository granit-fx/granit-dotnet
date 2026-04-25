using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Identity.Federated.Cognito.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.BackgroundJobs.Tests.Jobs;

/// <summary>
/// Metadata smoke tests — ensure the recurring-job contract declared on
/// <see cref="CognitoClientRoleSyncJob"/> stays aligned with ADR-030. The sync
/// service behaviour itself is covered by <c>CognitoClientRoleSyncServiceTests</c>.
/// </summary>
public sealed class CognitoClientRoleSyncJobTests
{
    [Fact]
    public void Job_declares_the_canonical_cron_schedule()
    {
        RecurringJobAttribute? attr = typeof(CognitoClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.CronExpression.ShouldBe("*/15 * * * *");
    }

    [Fact]
    public void Job_declares_the_canonical_job_name()
    {
        RecurringJobAttribute? attr = typeof(CognitoClientRoleSyncJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("cognito-client-role-sync");
    }

    [Fact]
    public void Job_implements_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(CognitoClientRoleSyncJob)).ShouldBeTrue();
}
