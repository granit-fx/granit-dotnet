using Granit.BackgroundJobs;
using Granit.BlobStorage.Jobs;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Jobs;

public sealed class OrphanBlobCleanupJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_hourly_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(OrphanBlobCleanupJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 * * * *");
        attr.Name.ShouldBe("blob-storage-orphan-cleanup");
    }

    [Fact]
    public void Should_implement_IBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(OrphanBlobCleanupJob)).ShouldBeTrue();
    }
}
