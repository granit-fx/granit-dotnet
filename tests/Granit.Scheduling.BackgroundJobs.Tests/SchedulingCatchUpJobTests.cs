using Granit.Scheduling.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.BackgroundJobs.Tests;

public sealed class SchedulingCatchUpJobTests
{

    [Fact]
    public void Job_ShouldImplementIBackgroundJob()
    {
        var job = new SchedulingCatchUpJob();
        job.ShouldBeAssignableTo<Granit.BackgroundJobs.IBackgroundJob>();
    }
}
