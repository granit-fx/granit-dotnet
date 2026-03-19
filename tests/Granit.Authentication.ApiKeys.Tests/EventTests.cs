using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Granit.Core.Events;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyCreatedEventTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyCreatedEvent(id, "Partner Key", ApiKeyType.Secret);

        evt.ApiKeyId.ShouldBe(id);
        evt.Name.ShouldBe("Partner Key");
        evt.Type.ShouldBe(ApiKeyType.Secret);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyCreatedEvent(id, "Key A", ApiKeyType.Publishable);
        var evt2 = new ApiKeyCreatedEvent(id, "Key A", ApiKeyType.Publishable);

        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void RecordInequality_DifferentType()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyCreatedEvent(id, "Key A", ApiKeyType.Secret);
        var evt2 = new ApiKeyCreatedEvent(id, "Key A", ApiKeyType.Publishable);

        evt1.ShouldNotBe(evt2);
    }
}

public sealed class ApiKeyRevokedEventTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyRevokedEvent(id, "abc123hash");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("abc123hash");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyRevokedEvent(id, "hash1");
        var evt2 = new ApiKeyRevokedEvent(id, "hash1");

        evt1.ShouldBe(evt2);
    }
}

public sealed class ApiKeyRotatedEventTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var evt = new ApiKeyRotatedEvent(oldId, newId, "oldhash");

        evt.OldApiKeyId.ShouldBe(oldId);
        evt.NewApiKeyId.ShouldBe(newId);
        evt.OldHashedKey.ShouldBe("oldhash");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var evt1 = new ApiKeyRotatedEvent(oldId, newId, "hash");
        var evt2 = new ApiKeyRotatedEvent(oldId, newId, "hash");

        evt1.ShouldBe(evt2);
    }
}

public sealed class ApiKeyScopesUpdatedEventTests
{
    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var evt = new ApiKeyScopesUpdatedEvent(id, "scopehash");

        evt.ApiKeyId.ShouldBe(id);
        evt.HashedKey.ShouldBe("scopehash");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var evt1 = new ApiKeyScopesUpdatedEvent(id, "hash");
        var evt2 = new ApiKeyScopesUpdatedEvent(id, "hash");

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

        IIntegrationEvent integrationEvent = entry.IntegrationEvents.ShouldHaveSingleItem();
        ApiKeyUsedEto eto = integrationEvent.ShouldBeOfType<ApiKeyUsedEto>();
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
