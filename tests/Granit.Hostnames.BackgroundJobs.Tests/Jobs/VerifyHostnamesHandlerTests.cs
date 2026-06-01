using Granit.BackgroundJobs;
using Granit.Hostnames.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.BackgroundJobs.Tests.Jobs;

public sealed class VerifyHostnamesHandlerTests
{
    [Fact]
    public void Job_is_a_recurring_background_job()
    {
        object[] attrs = typeof(VerifyHostnamesJob)
            .GetCustomAttributes(typeof(RecurringJobAttribute), inherit: false);

        attrs.ShouldNotBeEmpty();
    }
}
