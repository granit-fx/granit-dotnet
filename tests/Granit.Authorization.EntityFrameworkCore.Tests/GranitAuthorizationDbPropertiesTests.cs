using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class GranitAuthorizationDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_Default_IsAuth()
    {
        // Reset to default in case another test modified it.
        GranitAuthorizationDbProperties.DbTablePrefix = "auth_";

        GranitAuthorizationDbProperties.DbTablePrefix.ShouldBe("auth_");
    }

    [Fact]
    public void DbSchema_Default_IsNull()
    {
        // Reset to default in case another test modified it.
        GranitAuthorizationDbProperties.DbSchema = null;

        GranitAuthorizationDbProperties.DbSchema.ShouldBeNull();
    }

    [Fact]
    public void DbTablePrefix_CanBeModified()
    {
        string originalPrefix = GranitAuthorizationDbProperties.DbTablePrefix;

        try
        {
            GranitAuthorizationDbProperties.DbTablePrefix = "custom_";
            GranitAuthorizationDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitAuthorizationDbProperties.DbTablePrefix = originalPrefix;
        }
    }

    [Fact]
    public void DbSchema_CanBeModified()
    {
        string? originalSchema = GranitAuthorizationDbProperties.DbSchema;

        try
        {
            GranitAuthorizationDbProperties.DbSchema = "authz";
            GranitAuthorizationDbProperties.DbSchema.ShouldBe("authz");
        }
        finally
        {
            GranitAuthorizationDbProperties.DbSchema = originalSchema;
        }
    }
}
