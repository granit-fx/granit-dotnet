using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class GranitIdentityDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultsToIdentityFederatedUnderscore()
    {
        // Save original to restore after test
        string original = GranitIdentityDbProperties.DbTablePrefix;
        try
        {
            // Reset to default
            GranitIdentityDbProperties.DbTablePrefix = "identity_federated_";

            GranitIdentityDbProperties.DbTablePrefix.ShouldBe("identity_federated_");
        }
        finally
        {
            GranitIdentityDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_DefaultsToNull()
    {
        // Save original to restore after test
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
    public void DbSchema_IsSettable()
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
}
