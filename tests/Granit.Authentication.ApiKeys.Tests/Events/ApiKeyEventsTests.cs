using Granit.Authentication.ApiKeys.Events;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests.Events;

public sealed class ApiKeyEventsTests
{
    [Fact]
    public void ApiKeyCreatedEvent_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        ApiKeyCreatedEvent evt = new(id, "My Key", ApiKeyType.Secret);

        evt.ApiKeyId.ShouldBe(id);
        evt.Name.ShouldBe("My Key");
        evt.Type.ShouldBe(ApiKeyType.Secret);
    }

    [Fact]
    public void ApiKeyCreatedEvent_Equality_Works()
    {
        var id = Guid.NewGuid();
        ApiKeyCreatedEvent a = new(id, "Key", ApiKeyType.Secret);
        ApiKeyCreatedEvent b = new(id, "Key", ApiKeyType.Secret);

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
    public void ApiKeyScopesUpdatedEvent_HasCorrectProperties()
    {
        var id = Guid.NewGuid();
        ApiKeyScopesUpdatedEvent evt = new(id, "hash-xyz");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("hash-xyz");
    }

    [Fact]
    public void ApiKeyRotatedEvent_HasCorrectProperties()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        ApiKeyRotatedEvent evt = new(oldId, newId, "old-hash");

        evt.OldApiKeyId.ShouldBe(oldId);
        evt.NewApiKeyId.ShouldBe(newId);
        evt.OldHashedKey.ShouldBe("old-hash");
    }

    [Fact]
    public void ApiKeyRotatedEvent_Equality_DifferentIds_AreNotEqual()
    {
        ApiKeyRotatedEvent a = new(Guid.NewGuid(), Guid.NewGuid(), "hash");
        ApiKeyRotatedEvent b = new(Guid.NewGuid(), Guid.NewGuid(), "hash");

        a.ShouldNotBe(b);
    }
}
