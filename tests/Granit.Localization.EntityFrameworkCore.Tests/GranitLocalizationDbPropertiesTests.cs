using Shouldly;
using Xunit;

namespace Granit.Localization.EntityFrameworkCore.Tests;

public sealed class GranitLocalizationDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultsToLocalization() => GranitLocalizationDbProperties.DbTablePrefix.ShouldBe("localization_");

    [Fact]
    public void DbSchema_DefaultsToNull() => GranitLocalizationDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_CanBeChanged()
    {
        string originalPrefix = GranitLocalizationDbProperties.DbTablePrefix;
        try
        {
            GranitLocalizationDbProperties.DbTablePrefix = "i18n_";
            GranitLocalizationDbProperties.DbTablePrefix.ShouldBe("i18n_");
        }
        finally
        {
            GranitLocalizationDbProperties.DbTablePrefix = originalPrefix;
        }
    }

    [Fact]
    public void DbSchema_CanBeChanged()
    {
        string? originalSchema = GranitLocalizationDbProperties.DbSchema;
        try
        {
            GranitLocalizationDbProperties.DbSchema = "localization";
            GranitLocalizationDbProperties.DbSchema.ShouldBe("localization");
        }
        finally
        {
            GranitLocalizationDbProperties.DbSchema = originalSchema;
        }
    }
}
