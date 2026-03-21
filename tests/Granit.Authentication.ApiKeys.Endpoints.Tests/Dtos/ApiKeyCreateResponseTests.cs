using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests.Dtos;

public sealed class ApiKeyCreateResponseTests
{
    [Fact]
    public void Record_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        ApiKeyCreateResponse response = new(
            id,
            "gk_live_sk_abc123xyz",
            "gk_live_sk_",
            "3xyz",
            "My Key",
            ApiKeyType.Secret,
            "live",
            expiresAt);

        response.Id.ShouldBe(id);
        response.RawSecret.ShouldBe("gk_live_sk_abc123xyz");
        response.Prefix.ShouldBe("gk_live_sk_");
        response.LastFourChars.ShouldBe("3xyz");
        response.Name.ShouldBe("My Key");
        response.Type.ShouldBe(ApiKeyType.Secret);
        response.Environment.ShouldBe("live");
        response.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void Record_WithNullExpiresAt_HasNullExpiration()
    {
        ApiKeyCreateResponse response = new(
            Guid.NewGuid(), "raw", "prefix", "last", "name",
            ApiKeyType.Publishable, "test", null);

        response.ExpiresAt.ShouldBeNull();
    }
}
