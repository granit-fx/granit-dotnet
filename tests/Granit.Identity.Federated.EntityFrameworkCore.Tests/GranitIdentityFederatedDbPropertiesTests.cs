using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class GranitIdentityFederatedDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultsToIdentityFederatedUnderscore()
    {
        // Save original to restore after test
        string original = GranitIdentityFederatedDbProperties.DbTablePrefix;
        try
        {
            // Reset to default
            GranitIdentityFederatedDbProperties.DbTablePrefix = "identity_federated_";

            GranitIdentityFederatedDbProperties.DbTablePrefix.ShouldBe("identity_federated_");
        }
        finally
        {
            GranitIdentityFederatedDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_DefaultsToNull()
    {
        // Save original to restore after test
        string? original = GranitIdentityFederatedDbProperties.DbSchema;
        try
        {
            GranitIdentityFederatedDbProperties.DbSchema = null;

            GranitIdentityFederatedDbProperties.DbSchema.ShouldBeNull();
        }
        finally
        {
            GranitIdentityFederatedDbProperties.DbSchema = original;
        }
    }

    [Fact]
    public void DbTablePrefix_IsSettable()
    {
        string original = GranitIdentityFederatedDbProperties.DbTablePrefix;
        try
        {
            GranitIdentityFederatedDbProperties.DbTablePrefix = "custom_";

            GranitIdentityFederatedDbProperties.DbTablePrefix.ShouldBe("custom_");
        }
        finally
        {
            GranitIdentityFederatedDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_IsSettable()
    {
        string? original = GranitIdentityFederatedDbProperties.DbSchema;
        try
        {
            GranitIdentityFederatedDbProperties.DbSchema = "identity_schema";

            GranitIdentityFederatedDbProperties.DbSchema.ShouldBe("identity_schema");
        }
        finally
        {
            GranitIdentityFederatedDbProperties.DbSchema = original;
        }
    }
}
