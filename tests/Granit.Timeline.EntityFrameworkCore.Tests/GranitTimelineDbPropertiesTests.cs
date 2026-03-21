using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class GranitTimelineDbPropertiesTests
{
    [Fact]
    public void Default_DbTablePrefix_Is_timeline_() => GranitTimelineDbProperties.DbTablePrefix.ShouldBe("timeline_");

    [Fact]
    public void Default_DbSchema_IsNull() => GranitTimelineDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_CanBeSet()
    {
        string original = GranitTimelineDbProperties.DbTablePrefix;
        try
        {
            GranitTimelineDbProperties.DbTablePrefix = "custom_";
            GranitTimelineDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitTimelineDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_CanBeSet()
    {
        string? original = GranitTimelineDbProperties.DbSchema;
        try
        {
            GranitTimelineDbProperties.DbSchema = "my_schema";
            GranitTimelineDbProperties.DbSchema.ShouldBe("my_schema");
        }
        finally
        {
            GranitTimelineDbProperties.DbSchema = original;
        }
    }
}
