using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Internal;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyCurrentUserServiceAdditionalTests
{
    private readonly ApiKeyEntry _apiKey = ApiKeyEntry.Create(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "CI Pipeline Key",
        ApiKeyType.Publishable,
        "test",
        "dummy-hash",
        "gk_test_pk_",
        "efgh");

    [Fact]
    public void FirstName_ReturnsNull()
    {
        ApiKeyCurrentUserService sut = new(_apiKey);

        sut.FirstName.ShouldBeNull();
    }

    [Fact]
    public void LastName_ReturnsNull()
    {
        ApiKeyCurrentUserService sut = new(_apiKey);

        sut.LastName.ShouldBeNull();
    }

    [Fact]
    public void IsInRole_AlwaysReturnsFalse()
    {
        ApiKeyCurrentUserService sut = new(_apiKey);

        sut.IsInRole("admin").ShouldBeFalse();
        sut.IsInRole("user").ShouldBeFalse();
    }

    [Fact]
    public void UserName_ReturnsKeyName()
    {
        ApiKeyCurrentUserService sut = new(_apiKey);

        sut.UserName.ShouldBe("CI Pipeline Key");
    }

    [Fact]
    public void UserId_ReturnsApiKeyIdAsString()
    {
        ApiKeyCurrentUserService sut = new(_apiKey);

        sut.UserId.ShouldBe("22222222-2222-2222-2222-222222222222");
    }
}
