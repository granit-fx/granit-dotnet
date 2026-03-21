using Granit.BackgroundJobs.Domain;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Domain;

public sealed class RecurringJobRegistrationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        RecurringJobRegistration reg = new("my-job", "0 * * * *", "My.App.MyJob, My.App");

        reg.JobName.ShouldBe("my-job");
        reg.CronExpression.ShouldBe("0 * * * *");
        reg.MessageType.ShouldBe("My.App.MyJob, My.App");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        RecurringJobRegistration a = new("my-job", "0 * * * *", "My.App.MyJob, My.App");
        RecurringJobRegistration b = new("my-job", "0 * * * *", "My.App.MyJob, My.App");

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentJobName_AreNotEqual()
    {
        RecurringJobRegistration a = new("job-a", "0 * * * *", "My.App.MyJob, My.App");
        RecurringJobRegistration b = new("job-b", "0 * * * *", "My.App.MyJob, My.App");

        a.ShouldNotBe(b);
    }
}
