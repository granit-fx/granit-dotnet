using Granit.Scheduling.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.EntityFrameworkCore.Tests;

public sealed class SchedulingDbContextTests
{
    [Fact]
    public void ConfigureSchedulingModule_ShouldNotThrow()
    {
        var builder = new ModelBuilder();

        Should.NotThrow(() => builder.ConfigureSchedulingModule());
    }

    [Fact]
    public void DbProperties_DefaultPrefix_ShouldBeScheduling()
    {
        GranitSchedulingDbProperties.DbTablePrefix.ShouldBe("scheduling_");
    }
}
