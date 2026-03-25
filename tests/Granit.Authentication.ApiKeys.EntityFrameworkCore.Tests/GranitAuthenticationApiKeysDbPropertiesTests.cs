using Granit.Authentication.ApiKeys.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class GranitAuthenticationApiKeysDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultIsApiKeys()
    {
        // Reset to default in case other tests modified it
        GranitAuthenticationApiKeysDbProperties.DbTablePrefix = "authentication_api_keys_";

        GranitAuthenticationApiKeysDbProperties.DbTablePrefix.ShouldBe("authentication_api_keys_");
    }

    [Fact]
    public void DbSchema_DefaultIsNull()
    {
        // Reset to default in case other tests modified it
        GranitAuthenticationApiKeysDbProperties.DbSchema = null;

        GranitAuthenticationApiKeysDbProperties.DbSchema.ShouldBeNull();
    }

    [Fact]
    public void DbTablePrefix_CanBeOverridden()
    {
        string original = GranitAuthenticationApiKeysDbProperties.DbTablePrefix;
        try
        {
            GranitAuthenticationApiKeysDbProperties.DbTablePrefix = "custom_";

            GranitAuthenticationApiKeysDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitAuthenticationApiKeysDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_CanBeOverridden()
    {
        string? original = GranitAuthenticationApiKeysDbProperties.DbSchema;
        try
        {
            GranitAuthenticationApiKeysDbProperties.DbSchema = "apikeys";

            GranitAuthenticationApiKeysDbProperties.DbSchema.ShouldBe("apikeys");
        }
        finally
        {
            GranitAuthenticationApiKeysDbProperties.DbSchema = original;
        }
    }
}
