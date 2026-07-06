using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class GranitApiKeysDbPropertiesTests
{
    [Fact]
    public void DbSchema_ExplicitNull_SuppressesHostFallback()
    {
        string? original = GranitApiKeysDbProperties.DbSchema;
        try
        {
            // Explicitly assigning null must be honored as an override, NOT fall back to
            // GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema.
            GranitApiKeysDbProperties.DbSchema = null;

            GranitApiKeysDbProperties.DbSchema.ShouldBeNull();
        }
        finally
        {
            GranitApiKeysDbProperties.DbSchema = original;
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
