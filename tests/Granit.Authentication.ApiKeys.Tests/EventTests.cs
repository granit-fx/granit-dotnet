using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Granit.Core.Events;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyCreatedEtoTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyCreatedEto(id, "Partner Key", ApiKeyType.Secret);

        evt.ApiKeyId.ShouldBe(id);
        evt.Name.ShouldBe("Partner Key");
        evt.Type.ShouldBe(ApiKeyType.Secret);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyCreatedEto(id, "Key A", ApiKeyType.Publishable);
        var evt2 = new ApiKeyCreatedEto(id, "Key A", ApiKeyType.Publishable);

        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void RecordInequality_DifferentType()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyCreatedEto(id, "Key A", ApiKeyType.Secret);
        var evt2 = new ApiKeyCreatedEto(id, "Key A", ApiKeyType.Publishable);

        evt1.ShouldNotBe(evt2);
    }
}

public sealed class ApiKeyRevokedEtoTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyRevokedEto(id, "abc123hash");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("abc123hash");
    }

    [Fact]
    public void ImplementsIIntegrationEvent() =>
        new ApiKeyRevokedEto(Guid.NewGuid(), "hash")
            .ShouldBeAssignableTo<IIntegrationEvent>();

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyRevokedEto(id, "hash1");
        var evt2 = new ApiKeyRevokedEto(id, "hash1");

        evt1.ShouldBe(evt2);
    }
}

public sealed class ApiKeyRotatedEtoTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var evt = new ApiKeyRotatedEto(oldId, newId, "oldhash");

        evt.OldApiKeyId.ShouldBe(oldId);
        evt.NewApiKeyId.ShouldBe(newId);
        evt.OldHashedKey.ShouldBe("oldhash");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var evt1 = new ApiKeyRotatedEto(oldId, newId, "hash");
        var evt2 = new ApiKeyRotatedEto(oldId, newId, "hash");

        evt1.ShouldBe(evt2);
    }
}

public sealed class ApiKeyScopesUpdatedEtoTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyScopesUpdatedEto(id, "scopehash");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("scopehash");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyScopesUpdatedEto(id, "hash");
        var evt2 = new ApiKeyScopesUpdatedEto(id, "hash");

        evt1.ShouldBe(evt2);
    }
}

public sealed class ApiKeyUsedEtoTests
{
    [Fact]
    public void ImplementsIIntegrationEvent() =>
        new ApiKeyUsedEto(Guid.NewGuid(), "gk_live_sk_", DateTimeOffset.UtcNow)
            .ShouldBeAssignableTo<IIntegrationEvent>();

    [Fact]
    public void RecordUsage_RaisesApiKeyUsedEto()
    {
        DateTimeOffset usedAt = DateTimeOffset.UtcNow;
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(), "Test Key", ApiKeyType.Secret, "live",
            "hash", "gk_live_sk_", "abcd");

        entry.RecordUsage(usedAt);

        entry.IntegrationEvents.Count.ShouldBe(2); // ApiKeyCreatedEto + ApiKeyUsedEto
        ApiKeyUsedEto eto = entry.IntegrationEvents.OfType<ApiKeyUsedEto>().ShouldHaveSingleItem();
        eto.ApiKeyId.ShouldBe(entry.Id);
        eto.Prefix.ShouldBe("gk_live_sk_");
        eto.UsedAt.ShouldBe(usedAt);
    }
}

public sealed class ApiKeyExpiredEventTests
{
    [Fact]
    public void ImplementsIDomainEvent() =>
        new ApiKeyExpiredEvent(Guid.NewGuid(), "gk_live_sk_", DateTimeOffset.UtcNow)
            .ShouldBeAssignableTo<IDomainEvent>();

    [Fact]
    public void MarkAsExpired_RaisesApiKeyExpiredEvent()
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(), "Test Key", ApiKeyType.Secret, "live",
            "hash", "gk_live_sk_", "abcd");
        entry.SetExpiration(expiresAt);

        entry.MarkAsExpired();

        IDomainEvent domainEvent = entry.DomainEvents.ShouldHaveSingleItem();
        ApiKeyExpiredEvent evt = domainEvent.ShouldBeOfType<ApiKeyExpiredEvent>();
        evt.ApiKeyId.ShouldBe(entry.Id);
        evt.ExpiredAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void MarkAsExpired_NoExpiration_DoesNotEmitEvent()
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(), "Test Key", ApiKeyType.Secret, "live",
            "hash", "gk_live_sk_", "abcd");

        entry.MarkAsExpired();

        entry.DomainEvents.ShouldBeEmpty();
    }
}
