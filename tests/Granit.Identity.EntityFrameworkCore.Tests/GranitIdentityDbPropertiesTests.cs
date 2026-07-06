using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class GranitIdentityDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultsToIdentityUnderscore() =>
        // Assert the framework's chosen default WITHOUT assigning it first (no test in this
        // assembly mutates DbTablePrefix, so the initializer value is observable here).
        GranitIdentityDbProperties.DbTablePrefix.ShouldBe("identity_");

    [Fact]
    public void DbSchema_ExplicitSet_TakesPriority()
    {
        string? original = GranitIdentityDbProperties.DbSchema;
        try
        {
            GranitIdentityDbProperties.DbSchema = "identity_schema";

            GranitIdentityDbProperties.DbSchema.ShouldBe("identity_schema");
        }
        finally
        {
            GranitIdentityDbProperties.DbSchema = original;
        }
    }

    [Fact]
    public void DbSchema_AcceptsExplicitNull()
    {
        string? original = GranitIdentityDbProperties.DbSchema;
        try
        {
            GranitIdentityDbProperties.DbSchema = null;

            GranitIdentityDbProperties.DbSchema.ShouldBeNull();
        }
        finally
        {
            GranitIdentityDbProperties.DbSchema = original;
        }
    }
}
