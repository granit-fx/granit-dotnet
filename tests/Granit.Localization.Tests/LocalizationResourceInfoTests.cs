using Granit.Localization.Tests.TestResources;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class LocalizationResourceInfoTests
{
    [Fact]
    public void Constructor_SetsResourceTypeAndDefaultCulture()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.ResourceType.ShouldBe(typeof(TestResource));
        info.DefaultCulture.ShouldBe("fr");
    }

    [Fact]
    public void BaseTypes_IsInitializedEmpty()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.BaseTypes.ShouldNotBeNull();
        info.BaseTypes.ShouldBeEmpty();
    }

    [Fact]
    public void JsonSources_IsInitializedEmpty()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.JsonSources.ShouldNotBeNull();
        info.JsonSources.ShouldBeEmpty();
    }

    [Fact]
    public void AddJson_ReturnsSameInstance_ForFluentChaining()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        LocalizationResourceInfo result = info.AddJson(typeof(TestResource).Assembly, "Some.Prefix");

        result.ShouldBeSameAs(info);
    }

    [Fact]
    public void AddJson_AddsMultipleSources()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.AddJson(typeof(TestResource).Assembly, "Prefix1");
        info.AddJson(typeof(TestResource).Assembly, "Prefix2");

        info.JsonSources.Count.ShouldBe(2);
    }

    [Fact]
    public void AddBaseTypes_ReturnsSameInstance_ForFluentChaining()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        LocalizationResourceInfo result = info.AddBaseTypes(typeof(ParentTestResource));

        result.ShouldBeSameAs(info);
    }

    [Fact]
    public void AddBaseTypes_AddsMultipleTypes()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.AddBaseTypes(typeof(ParentTestResource), typeof(ChildTestResource));

        info.BaseTypes.Count.ShouldBe(2);
        info.BaseTypes.ShouldContain(typeof(ParentTestResource));
        info.BaseTypes.ShouldContain(typeof(ChildTestResource));
    }

    [Fact]
    public void AddBaseTypes_CanBeCalledMultipleTimes()
    {
        LocalizationResourceInfo info = new(typeof(TestResource), "fr");

        info.AddBaseTypes(typeof(ParentTestResource));
        info.AddBaseTypes(typeof(ChildTestResource));

        info.BaseTypes.Count.ShouldBe(2);
    }
}
