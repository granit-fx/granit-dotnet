using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests;

public sealed class GranitQueryEngineDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_default_is_query_engine_underscore() => GranitQueryEngineDbProperties.DbTablePrefix.ShouldBe("query_engine_");

    [Fact]
    public void DbSchema_default_is_null() => GranitQueryEngineDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_can_be_set()
    {
        string original = GranitQueryEngineDbProperties.DbTablePrefix;
        try
        {
            GranitQueryEngineDbProperties.DbTablePrefix = "custom_";
            GranitQueryEngineDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitQueryEngineDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_can_be_set()
    {
        string? original = GranitQueryEngineDbProperties.DbSchema;
        try
        {
            GranitQueryEngineDbProperties.DbSchema = "custom_schema";
            GranitQueryEngineDbProperties.DbSchema.ShouldBe("custom_schema");
        }
        finally
        {
            GranitQueryEngineDbProperties.DbSchema = original;
        }
    }
}
