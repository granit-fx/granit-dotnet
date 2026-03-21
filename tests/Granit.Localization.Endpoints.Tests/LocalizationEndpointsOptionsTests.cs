using Granit.Localization.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class LocalizationEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_DefaultsToLocalization()
    {
        LocalizationEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("localization");
    }

    [Fact]
    public void RoutePrefix_CanBeChanged()
    {
        LocalizationEndpointsOptions options = new()
        {
            RoutePrefix = "i18n",
        };

        options.RoutePrefix.ShouldBe("i18n");
    }

    [Fact]
    public void TagName_DefaultsToLocalization()
    {
        LocalizationEndpointsOptions options = new();

        options.TagName.ShouldBe("Localization");
    }

    [Fact]
    public void TagName_CanBeChanged()
    {
        LocalizationEndpointsOptions options = new()
        {
            TagName = "Translations",
        };

        options.TagName.ShouldBe("Translations");
    }
}
