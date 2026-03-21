using Granit.Http.Cookies.Internal;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class ThirdPartyServiceRegistryTests
{
    [Fact]
    public void GetAll_ReturnsAllServices()
    {
        List<ThirdPartyServiceDefinition> services =
        [
            new("matomo", CookieCategory.Analytics, ["^_pk_"]),
            new("hubspot", CookieCategory.Marketing, ["^__hs"]),
        ];
        ThirdPartyServiceRegistry sut = new(services);

        IReadOnlyList<ThirdPartyServiceDefinition> result = sut.GetAll();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void GetByCategory_ReturnsMatchingServices()
    {
        List<ThirdPartyServiceDefinition> services =
        [
            new("matomo", CookieCategory.Analytics, ["^_pk_"]),
            new("hotjar", CookieCategory.Analytics, ["^_hj"]),
            new("hubspot", CookieCategory.Marketing, ["^__hs"]),
        ];
        ThirdPartyServiceRegistry sut = new(services);

        IReadOnlyList<ThirdPartyServiceDefinition> result = sut.GetByCategory(CookieCategory.Analytics);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void GetByCategory_NoMatch_ReturnsEmpty()
    {
        List<ThirdPartyServiceDefinition> services =
        [
            new("matomo", CookieCategory.Analytics, ["^_pk_"]),
        ];
        ThirdPartyServiceRegistry sut = new(services);

        IReadOnlyList<ThirdPartyServiceDefinition> result = sut.GetByCategory(CookieCategory.Marketing);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetAll_EmptyList_ReturnsEmpty()
    {
        ThirdPartyServiceRegistry sut = new([]);

        IReadOnlyList<ThirdPartyServiceDefinition> result = sut.GetAll();

        result.ShouldBeEmpty();
    }
}
