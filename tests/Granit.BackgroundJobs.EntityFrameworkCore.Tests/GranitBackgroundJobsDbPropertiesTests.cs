using Granit.BackgroundJobs.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

public sealed class GranitBackgroundJobsDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_Default_IsBackgroundJobsUnderscore() => GranitBackgroundJobsDbProperties.DbTablePrefix.ShouldBe("background_jobs_");

    [Fact]
    public void DbSchema_Default_IsNull() => GranitBackgroundJobsDbProperties.DbSchema.ShouldBeNull();
}
