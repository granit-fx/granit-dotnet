using Shouldly;
using Xunit;

namespace Granit.Querying.EntityFrameworkCore.Tests;

public sealed class GranitQueryingDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_default_is_querying_underscore() => GranitQueryingDbProperties.DbTablePrefix.ShouldBe("querying_");

    [Fact]
    public void DbSchema_default_is_null() => GranitQueryingDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_can_be_set()
    {
        string original = GranitQueryingDbProperties.DbTablePrefix;
        try
        {
            GranitQueryingDbProperties.DbTablePrefix = "custom_";
            GranitQueryingDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitQueryingDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_can_be_set()
    {
        string? original = GranitQueryingDbProperties.DbSchema;
        try
        {
            GranitQueryingDbProperties.DbSchema = "custom_schema";
            GranitQueryingDbProperties.DbSchema.ShouldBe("custom_schema");
        }
        finally
        {
            GranitQueryingDbProperties.DbSchema = original;
        }
    }
}
