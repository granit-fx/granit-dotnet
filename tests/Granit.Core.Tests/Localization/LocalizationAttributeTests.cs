using Granit.Core.Localization;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Localization;

public sealed class LocalizationAttributeTests
{
    // -------------------------------------------------------------------------
    // LocalizationResourceNameAttribute
    // -------------------------------------------------------------------------

    [Fact]
    public void LocalizationResourceName_SetsName()
    {
        LocalizationResourceNameAttribute attr = new("Granit");

        attr.Name.ShouldBe("Granit");
    }

    [Fact]
    public void LocalizationResourceName_DefaultCulture_IsEn()
    {
        LocalizationResourceNameAttribute attr = new("Test");

        attr.DefaultCulture.ShouldBe("en");
    }

    [Fact]
    public void LocalizationResourceName_DefaultCulture_CanBeOverridden()
    {
        LocalizationResourceNameAttribute attr = new("Test") { DefaultCulture = "fr" };

        attr.DefaultCulture.ShouldBe("fr");
    }

    [Fact]
    public void LocalizationResourceName_DoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(LocalizationResourceNameAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void LocalizationResourceName_IsInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(LocalizationResourceNameAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.Inherited.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // InheritResourceAttribute
    // -------------------------------------------------------------------------

    [Fact]
    public void InheritResource_StoresBaseResourceTypes()
    {
        InheritResourceAttribute attr = new(typeof(string), typeof(int));

        attr.BaseResourceTypes.Length.ShouldBe(2);
        attr.BaseResourceTypes.ShouldContain(typeof(string));
        attr.BaseResourceTypes.ShouldContain(typeof(int));
    }

    [Fact]
    public void InheritResource_NoTypes_HasEmptyArray()
    {
        InheritResourceAttribute attr = new();

        attr.BaseResourceTypes.ShouldBeEmpty();
    }

    [Fact]
    public void InheritResource_AllowsMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(InheritResourceAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeTrue();
    }

    [Fact]
    public void InheritResource_IsInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(InheritResourceAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.Inherited.ShouldBeTrue();
    }
}
