using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class GranitIdentityDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultsToGranitIdentityUnderscore()
    {
        string original = GranitIdentityDbProperties.DbTablePrefix;
        try
        {
            GranitIdentityDbProperties.DbTablePrefix = "granit_identity_";

            GranitIdentityDbProperties.DbTablePrefix.ShouldBe("granit_identity_");
        }
        finally
        {
            GranitIdentityDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbTablePrefix_IsSettable()
    {
        string original = GranitIdentityDbProperties.DbTablePrefix;
        try
        {
            GranitIdentityDbProperties.DbTablePrefix = "custom_";

            GranitIdentityDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitIdentityDbProperties.DbTablePrefix = original;
        }
    }

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
