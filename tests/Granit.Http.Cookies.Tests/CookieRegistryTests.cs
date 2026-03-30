using Granit.Http.Cookies.Internal;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieRegistryTests
{
    private readonly CookieRegistry _sut = new();

    private static CookieDefinition CreateDefinition(
        string name = "test_cookie",
        CookieCategory category = CookieCategory.Analytics) =>
        new(name, category, 365, true, "Test purpose");

    [Fact]
    public void Register_AddsDefinition()
    {
        CookieDefinition definition = CreateDefinition();

        _sut.Register(definition);

        _sut.IsRegistered("test_cookie").ShouldBeTrue();
    }

    [Fact]
    public void Register_SameDefinitionTwice_IsIdempotent()
    {
        CookieDefinition definition = CreateDefinition();
        _sut.Register(definition);

        _sut.Register(definition);

        _sut.GetDefinition("test_cookie").ShouldBe(definition);
    }

    [Fact]
    public void Register_DifferentDefinitionSameName_ThrowsInvalidOperationException()
    {
        _sut.Register(CreateDefinition());
        CookieDefinition different = new("test_cookie", CookieCategory.Marketing, 30, false, "Different");

        Action act = () => _sut.Register(different);

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("different definition");
    }

    [Fact]
    public void GetDefinition_ExistingCookie_ReturnsDefinition()
    {
        CookieDefinition definition = CreateDefinition();
        _sut.Register(definition);

        CookieDefinition? result = _sut.GetDefinition("test_cookie");

        result.ShouldBe(definition);
    }

    [Fact]
    public void GetDefinition_UnknownCookie_ReturnsNull()
    {
        CookieDefinition? result = _sut.GetDefinition("unknown");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetDefinition_IsCaseInsensitive()
    {
        CookieDefinition definition = CreateDefinition();
        _sut.Register(definition);

        CookieDefinition? result = _sut.GetDefinition("TEST_COOKIE");

        result.ShouldBe(definition);
    }

    [Fact]
    public void GetByCategory_ReturnsMatchingCookies()
    {
        _sut.Register(new("analytics_1", CookieCategory.Analytics, 365, false, "Analytics 1"));
        _sut.Register(new("analytics_2", CookieCategory.Analytics, 365, false, "Analytics 2"));
        _sut.Register(new("session", CookieCategory.StrictlyNecessary, 1, true, "Session"));

        IReadOnlyList<CookieDefinition> result = _sut.GetByCategory(CookieCategory.Analytics);

        result.Count.ShouldBe(2);
        result.ToList().ForEach(c => c.Category.ShouldBe(CookieCategory.Analytics));
    }

    [Fact]
    public void GetByCategory_NoMatches_ReturnsEmpty()
    {
        _sut.Register(CreateDefinition());

        IReadOnlyList<CookieDefinition> result = _sut.GetByCategory(CookieCategory.Marketing);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void IsRegistered_ReturnsTrueForRegistered()
    {
        _sut.Register(CreateDefinition());

        _sut.IsRegistered("test_cookie").ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_ReturnsFalseForUnknown() =>
        _sut.IsRegistered("unknown").ShouldBeFalse();

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        _sut.Register(new("cookie_1", CookieCategory.Analytics, 365, false, "Cookie 1"));
        _sut.Register(new("cookie_2", CookieCategory.Preferences, 180, true, "Cookie 2"));
        _sut.Register(new("cookie_3", CookieCategory.StrictlyNecessary, 1, true, "Cookie 3"));

        IReadOnlyList<CookieDefinition> result = _sut.GetAll();

        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Register_NullDefinition_ThrowsArgumentNullException()
    {
        Action act = () => _sut.Register(null!);

        Should.Throw<ArgumentNullException>(act);
    }
}
