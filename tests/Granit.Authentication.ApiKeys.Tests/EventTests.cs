using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyRevokedEtoTests
{
    [Fact]
    public void ImplementsIIntegrationEvent() =>
        new ApiKeyRevokedEto(Guid.NewGuid(), "hash")
            .ShouldBeAssignableTo<IIntegrationEvent>();
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
