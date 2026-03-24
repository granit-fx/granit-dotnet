using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests;

public sealed class FeaturesLocalizationResourceTests
{
    [Fact]
    public void LocalizationResourceNameAttribute_HasName_Features()
    {
        var attribute =
            (LocalizationResourceNameAttribute?)Attribute.GetCustomAttribute(
                typeof(FeaturesLocalizationResource),
                typeof(LocalizationResourceNameAttribute));

        attribute.ShouldNotBeNull();
        attribute!.Name.ShouldBe("Features");
    }

    [Fact]
    public void InheritResourceAttribute_InheritsFrom_GranitLocalizationResource()
    {
        var attribute =
            (InheritResourceAttribute?)Attribute.GetCustomAttribute(
                typeof(FeaturesLocalizationResource),
                typeof(InheritResourceAttribute));

        attribute.ShouldNotBeNull();
        attribute!.BaseResourceTypes.ShouldContain(typeof(GranitLocalizationResource));
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(FeaturesLocalizationResource).IsSealed.ShouldBeTrue();
}
