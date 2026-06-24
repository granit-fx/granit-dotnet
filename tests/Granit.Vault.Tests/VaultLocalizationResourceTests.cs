using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests;

public sealed class VaultLocalizationResourceTests
{
    [Fact]
    public void Class_HasLocalizationResourceNameAttribute()
    {
        var attribute = (LocalizationResourceNameAttribute?)
            Attribute.GetCustomAttribute(typeof(VaultLocalizationResource), typeof(LocalizationResourceNameAttribute));

        attribute.ShouldNotBeNull();
    }

    [Fact]
    public void ResourceName_IsVault()
    {
        var attribute = (LocalizationResourceNameAttribute)
            Attribute.GetCustomAttribute(typeof(VaultLocalizationResource), typeof(LocalizationResourceNameAttribute))!;

        attribute.Name.ShouldBe("Vault");
    }

    [Fact]
    public void DefaultCulture_IsEnglish()
    {
        var attribute = (LocalizationResourceNameAttribute)
            Attribute.GetCustomAttribute(typeof(VaultLocalizationResource), typeof(LocalizationResourceNameAttribute))!;

        attribute.DefaultCulture.ShouldBe("en");
    }

    [Fact]
    public void Class_IsSealed() => typeof(VaultLocalizationResource).IsSealed.ShouldBeTrue();
}
