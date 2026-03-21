using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class BackgroundJobStatusTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        DateTimeOffset lastExecuted = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset nextExecution = new(2026, 1, 16, 8, 0, 0, TimeSpan.Zero);

        BackgroundJobStatus status = new(
            JobName: "daily-report",
            CronExpression: "0 8 * * *",
            IsEnabled: true,
            LastExecutedAt: lastExecuted,
            NextExecutionAt: nextExecution,
            ConsecutiveFailures: 2,
            DeadLetterCount: 5,
            LastError: "timeout");

        status.JobName.ShouldBe("daily-report");
        status.CronExpression.ShouldBe("0 8 * * *");
        status.IsEnabled.ShouldBeTrue();
        status.LastExecutedAt.ShouldBe(lastExecuted);
        status.NextExecutionAt.ShouldBe(nextExecution);
        status.ConsecutiveFailures.ShouldBe(2);
        status.DeadLetterCount.ShouldBe(5);
        status.LastError.ShouldBe("timeout");
    }

    [Fact]
    public void Constructor_NullableFieldsCanBeNull()
    {
        BackgroundJobStatus status = new(
            JobName: "test",
            CronExpression: "0 * * * *",
            IsEnabled: false,
            LastExecutedAt: null,
            NextExecutionAt: null,
            ConsecutiveFailures: 0,
            DeadLetterCount: 0,
            LastError: null);

        status.LastExecutedAt.ShouldBeNull();
        status.NextExecutionAt.ShouldBeNull();
        status.LastError.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        BackgroundJobStatus a = new("job", "0 * * * *", true, null, null, 0, 0, null);
        BackgroundJobStatus b = new("job", "0 * * * *", true, null, null, 0, 0, null);

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentJobName_AreNotEqual()
    {
        BackgroundJobStatus a = new("job-a", "0 * * * *", true, null, null, 0, 0, null);
        BackgroundJobStatus b = new("job-b", "0 * * * *", true, null, null, 0, 0, null);

        a.ShouldNotBe(b);
    }
}
