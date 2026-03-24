using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Tests.Localization;

public sealed class LocalizableStringTests
{
    private sealed class TestResource;

    [Fact]
    public void Fixed_Localize_ReturnsValueAsIs()
    {
        var sut = LocalizableString.Fixed("Hello World");

        sut.Localize(null).ShouldBe("Hello World");
    }

    [Fact]
    public void Fixed_Localize_WithNullFactory_ReturnsValue()
    {
        var sut = LocalizableString.Fixed("fixed-value");

        sut.Localize(null).ShouldBe("fixed-value");
    }

    [Fact]
    public void Create_WithNullFactory_ReturnsKey()
    {
        var sut = LocalizableString.Create<TestResource>("Permission:Users:Read");

        sut.Localize(null).ShouldBe("Permission:Users:Read");
    }

    [Fact]
    public void Fixed_DifferentValues_ReturnDifferentResults()
    {
        var a = LocalizableString.Fixed("Alpha");
        var b = LocalizableString.Fixed("Beta");

        a.Localize(null).ShouldBe("Alpha");
        b.Localize(null).ShouldBe("Beta");
    }

    [Fact]
    public void Create_DifferentKeys_ReturnDifferentKeys_WhenNoFactory()
    {
        var a = LocalizableString.Create<TestResource>("Key1");
        var b = LocalizableString.Create<TestResource>("Key2");

        a.Localize(null).ShouldBe("Key1");
        b.Localize(null).ShouldBe("Key2");
    }

    [Fact]
    public void Fixed_EmptyString_ReturnsEmptyString()
    {
        var sut = LocalizableString.Fixed("");

        sut.Localize(null).ShouldBe("");
    }
}
