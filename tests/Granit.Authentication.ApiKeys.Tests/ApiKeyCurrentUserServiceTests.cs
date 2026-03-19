using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Internal;
using Granit.Security;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyCurrentUserServiceTests
{
    private readonly ApiKeyEntry _apiKey = ApiKeyEntry.Create(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Partenaire Labo X",
        ApiKeyType.Secret,
        "live",
        "dummy-hash",
        "gk_live_sk_",
        "abcd");

    private readonly ApiKeyCurrentUserService _sut;

    public ApiKeyCurrentUserServiceTests() => _sut = new ApiKeyCurrentUserService(_apiKey);

    [Fact]
    public void ActorKind_ReturnsExternalSystem() => _sut.ActorKind.ShouldBe(ActorKind.ExternalSystem);

    [Fact]
    public void IsMachine_ReturnsTrue() => _sut.IsMachine.ShouldBeTrue();

    [Fact]
    public void UserId_ReturnsApiKeyId() => _sut.UserId.ShouldBe(_apiKey.Id.ToString());

    [Fact]
    public void UserName_ReturnsApiKeyName() => _sut.UserName.ShouldBe("Partenaire Labo X");

    [Fact]
    public void ApiKeyId_ReturnsId() => _sut.ApiKeyId.ShouldBe(_apiKey.Id);

    [Fact]
    public void IsAuthenticated_ReturnsTrue() => _sut.IsAuthenticated.ShouldBeTrue();

    [Fact]
    public void Email_ReturnsNull() => _sut.Email.ShouldBeNull();

    [Fact]
    public void GetRoles_ReturnsEmpty() => _sut.GetRoles().ShouldBeEmpty();

    [Fact]
    public void Constructor_ThrowsOnNull() => Should.Throw<ArgumentNullException>(() => new ApiKeyCurrentUserService(null!));
}
