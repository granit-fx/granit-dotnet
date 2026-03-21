using Granit.Authentication.ApiKeys.Events;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests.Events;

public sealed class ApiKeyEventsTests
{
    [Fact]
    public void ApiKeyCreatedEto_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        ApiKeyCreatedEto evt = new(id, "My Key", ApiKeyType.Secret);

        evt.ApiKeyId.ShouldBe(id);
        evt.Name.ShouldBe("My Key");
        evt.Type.ShouldBe(ApiKeyType.Secret);
    }

    [Fact]
    public void ApiKeyCreatedEto_Equality_Works()
    {
        var id = Guid.NewGuid();
        ApiKeyCreatedEto a = new(id, "Key", ApiKeyType.Secret);
        ApiKeyCreatedEto b = new(id, "Key", ApiKeyType.Secret);

        a.ShouldBe(b);
    }

    [Fact]
    public void ApiKeyRevokedEvent_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        ApiKeyRevokedEvent evt = new(id, "hash-abc");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("hash-abc");
    }

    [Fact]
    public void ApiKeyScopesUpdatedEto_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        ApiKeyScopesUpdatedEto evt = new(id, "hash-xyz");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("hash-xyz");
    }

    [Fact]
    public void ApiKeyRotatedEto_HasCorrectProperties()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        ApiKeyRotatedEto evt = new(oldId, newId, "old-hash");

        evt.OldApiKeyId.ShouldBe(oldId);
        evt.NewApiKeyId.ShouldBe(newId);
        evt.OldHashedKey.ShouldBe("old-hash");
    }

    [Fact]
    public void ApiKeyRotatedEto_Equality_DifferentIds_AreNotEqual()
    {
        ApiKeyRotatedEto a = new(Guid.NewGuid(), Guid.NewGuid(), "hash");
        ApiKeyRotatedEto b = new(Guid.NewGuid(), Guid.NewGuid(), "hash");

        a.ShouldNotBe(b);
    }
}
