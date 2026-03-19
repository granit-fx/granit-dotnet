using Granit.BackgroundJobs;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Wolverine.Tests;

public sealed class CleanupOrphanBlobsCommandTests
{
    [Fact]
    public void Command_should_have_RecurringJob_attribute_with_hourly_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(CleanupOrphanBlobsCommand), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 * * * *");
        attr.Name.ShouldBe("blob-orphan-cleanup");
    }
}
