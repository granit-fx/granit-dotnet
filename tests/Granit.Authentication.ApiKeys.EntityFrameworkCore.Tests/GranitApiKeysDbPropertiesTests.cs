using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class GranitApiKeysDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultIsApiKeys()
    {
        // Reset to default in case other tests modified it
        GranitApiKeysDbProperties.DbTablePrefix = "api_keys_";

        GranitApiKeysDbProperties.DbTablePrefix.ShouldBe("api_keys_");
    }

    [Fact]
    public void DbSchema_DefaultIsNull()
    {
        // Reset to default in case other tests modified it
        GranitApiKeysDbProperties.DbSchema = null;

        GranitApiKeysDbProperties.DbSchema.ShouldBeNull();
    }

    [Fact]
    public void DbTablePrefix_CanBeOverridden()
    {
        string original = GranitApiKeysDbProperties.DbTablePrefix;
        try
        {
            GranitApiKeysDbProperties.DbTablePrefix = "custom_";

            GranitApiKeysDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitApiKeysDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_CanBeOverridden()
    {
        string? original = GranitApiKeysDbProperties.DbSchema;
        try
        {
            GranitApiKeysDbProperties.DbSchema = "apikeys";

            GranitApiKeysDbProperties.DbSchema.ShouldBe("apikeys");
        }
        finally
        {
            GranitApiKeysDbProperties.DbSchema = original;
        }
    }
}
