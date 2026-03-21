using Granit.Core.Localization;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

using MsLocalizedString = Microsoft.Extensions.Localization.LocalizedString;

namespace Granit.Core.Tests.Localization;

public sealed class LocalizedStringWithFactoryTests
{
    private sealed class TestResource;

    // -------------------------------------------------------------------------
    // Create<T> — with real IStringLocalizerFactory
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithFactory_ResolvesLocalizedValue()
    {
        IStringLocalizerFactory factory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer localizer = Substitute.For<IStringLocalizer>();
        factory.Create(typeof(TestResource)).Returns(localizer);
        localizer["Permission:Users:Read"].Returns(
            new MsLocalizedString("Permission:Users:Read", "Read Users"));

        var sut = LocalizableString.Create<TestResource>("Permission:Users:Read");

        string result = sut.Localize(factory);

        result.ShouldBe("Read Users");
    }

    [Fact]
    public void Create_WithFactory_DifferentKey_ResolvesDifferentValue()
    {
        IStringLocalizerFactory factory = Substitute.For<IStringLocalizerFactory>();
        IStringLocalizer localizer = Substitute.For<IStringLocalizer>();
        factory.Create(typeof(TestResource)).Returns(localizer);
        localizer["Key1"].Returns(new MsLocalizedString("Key1", "Value 1"));
        localizer["Key2"].Returns(new MsLocalizedString("Key2", "Value 2"));

        var sut1 = LocalizableString.Create<TestResource>("Key1");
        var sut2 = LocalizableString.Create<TestResource>("Key2");

        sut1.Localize(factory).ShouldBe("Value 1");
        sut2.Localize(factory).ShouldBe("Value 2");
    }

    // -------------------------------------------------------------------------
    // Fixed — ignores factory
    // -------------------------------------------------------------------------

    [Fact]
    public void Fixed_WithFactory_IgnoresFactoryAndReturnsValue()
    {
        IStringLocalizerFactory factory = Substitute.For<IStringLocalizerFactory>();
        var sut = LocalizableString.Fixed("constant value");

        string result = sut.Localize(factory);

        result.ShouldBe("constant value");
        factory.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }
}
