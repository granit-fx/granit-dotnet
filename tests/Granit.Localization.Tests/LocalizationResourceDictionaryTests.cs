using Granit.Localization.Tests.TestResources;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class LocalizationResourceStoreTests
{
    [Fact]
    public void Add_RegistersResource()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();

        // Act
        LocalizationResourceInfo info = dictionary.Add<TestResource>("fr");

        // Assert
        info.ShouldNotBeNull();
        info.ResourceType.ShouldBe(typeof(TestResource));
        info.DefaultCulture.ShouldBe("fr");
    }

    [Fact]
    public void Get_ReturnsRegisteredResource()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();
        dictionary.Add<TestResource>("fr");

        // Act
        LocalizationResourceInfo info = dictionary.Get<TestResource>();

        // Assert
        info.ResourceType.ShouldBe(typeof(TestResource));
    }

    [Fact]
    public void Get_ThrowsForUnregisteredResource()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();

        // Act
        Action act = () => dictionary.Get<TestResource>();

        // Assert
        Should.Throw<KeyNotFoundException>(act);
    }

    [Fact]
    public void TryGetValue_ReturnsTrueForRegistered()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();
        dictionary.Add<TestResource>("fr");

        // Act
        bool found = dictionary.TryGetValue(typeof(TestResource), out LocalizationResourceInfo? info);

        // Assert
        found.ShouldBeTrue();
        info.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetValue_ReturnsFalseForUnregistered()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();

        // Act
        bool found = dictionary.TryGetValue(typeof(TestResource), out LocalizationResourceInfo? info);

        // Assert
        found.ShouldBeFalse();
        info.ShouldBeNull();
    }

    [Fact]
    public void Add_Duplicate_ReplacesExisting()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();
        dictionary.Add<TestResource>("fr");

        // Act
        _ = dictionary.Add<TestResource>("en");

        // Assert
        dictionary.Get<TestResource>().DefaultCulture.ShouldBe("en");
    }

    [Fact]
    public void GetAll_ReturnsAllRegisteredResources()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();
        dictionary.Add<TestResource>("fr");
        dictionary.Add<ParentTestResource>("fr");

        // Act
        var all = dictionary.GetAll().ToList();

        // Assert
        all.Count.ShouldBe(2);
    }

    [Fact]
    public void AddJson_FluentChaining_AddsSource()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();

        // Act
        LocalizationResourceInfo info = dictionary.Add<TestResource>("fr")
            .AddJson(typeof(TestResource).Assembly, "Some.Prefix");

        // Assert
        info.JsonSources.Count.ShouldBe(1);
    }

    [Fact]
    public void AddBaseTypes_FluentChaining_AddsBaseTypes()
    {
        // Arrange
        LocalizationResourceStore dictionary = new();

        // Act
        LocalizationResourceInfo info = dictionary.Add<TestResource>("fr")
            .AddBaseTypes(typeof(ParentTestResource));

        // Assert
        info.BaseTypes.Count.ShouldBe(1);
        info.BaseTypes.ShouldContain(typeof(ParentTestResource));
    }
}
