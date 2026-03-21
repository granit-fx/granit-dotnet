using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class GranitFeaturesDbPropertiesTests
{
    [Fact]
    public void Default_DbTablePrefix_IsFeatureUnderscore() => GranitFeaturesDbProperties.DbTablePrefix.ShouldBe("feature_");

    [Fact]
    public void Default_DbSchema_IsNull() => GranitFeaturesDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_CanBeChanged()
    {
        string original = GranitFeaturesDbProperties.DbTablePrefix;
        try
        {
            GranitFeaturesDbProperties.DbTablePrefix = "feat_";
            GranitFeaturesDbProperties.DbTablePrefix.ShouldBe("feat_");
        }
        finally
        {
            GranitFeaturesDbProperties.DbTablePrefix = original;
        }
    }

    [Fact]
    public void DbSchema_CanBeChanged()
    {
        string? original = GranitFeaturesDbProperties.DbSchema;
        try
        {
            GranitFeaturesDbProperties.DbSchema = "features";
            GranitFeaturesDbProperties.DbSchema.ShouldBe("features");
        }
        finally
        {
            GranitFeaturesDbProperties.DbSchema = original;
        }
    }
}
