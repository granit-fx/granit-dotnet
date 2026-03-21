using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests.Dtos;

public sealed class ApiKeyRotateResponseTests
{
    [Fact]
    public void Record_HasCorrectProperties()
    {
        var newKeyId = Guid.NewGuid();
        var oldKeyId = Guid.NewGuid();

        ApiKeyRotateResponse response = new(
            newKeyId,
            "gk_live_sk_newkey123",
            "gk_live_sk_",
            "y123",
            oldKeyId);

        response.NewKeyId.ShouldBe(newKeyId);
        response.RawSecret.ShouldBe("gk_live_sk_newkey123");
        response.Prefix.ShouldBe("gk_live_sk_");
        response.LastFourChars.ShouldBe("y123");
        response.OldKeyId.ShouldBe(oldKeyId);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var newId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        ApiKeyRotateResponse a = new(newId, "raw", "prefix", "last", oldId);
        ApiKeyRotateResponse b = new(newId, "raw", "prefix", "last", oldId);

        a.ShouldBe(b);
    }
}
