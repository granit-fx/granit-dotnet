using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.Events;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Tests;

public sealed class DocumentPublicLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 14, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Hash = [1, 2, 3, 4];

    private static FakeTimeProvider Time(DateTimeOffset? at = null) => new(at ?? Now);

    private static DocumentPublicLink NewActive(TimeProvider time, TimeSpan? ttl = null, int? maxUses = null) =>
        DocumentPublicLink.Create(
            id: Guid.NewGuid(),
            documentId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            tokenHash: Hash,
            scope: PublicLinkScope.Download,
            expiresAt: time.GetUtcNow() + (ttl ?? TimeSpan.FromDays(1)),
            maxUses: maxUses,
            timeProvider: time);

    [Fact]
    public void Create_RaisesCreatedEvent()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time);

        link.DomainEvents.Count.ShouldBe(1);
        DocumentPublicLinkCreatedEvent evt = link.DomainEvents.OfType<DocumentPublicLinkCreatedEvent>().Single();
        evt.LinkId.ShouldBe(link.Id);
        evt.DocumentId.ShouldBe(link.DocumentId);
        evt.Scope.ShouldBe(PublicLinkScope.Download);
        evt.CreatedAt.ShouldBe(Now);
        link.CurrentUses.ShouldBe(0);
        link.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_RejectsPastExpiry()
    {
        FakeTimeProvider time = Time();
        Should.Throw<ArgumentException>(() => DocumentPublicLink.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Hash, PublicLinkScope.View,
            expiresAt: Now - TimeSpan.FromMinutes(1),
            maxUses: null,
            timeProvider: time));
    }

    [Fact]
    public void Create_RejectsNegativeMaxUses()
    {
        FakeTimeProvider time = Time();
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentPublicLink.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Hash, PublicLinkScope.Download,
            expiresAt: Now + TimeSpan.FromHours(1),
            maxUses: 0,
            timeProvider: time));
    }

    [Fact]
    public void Revoke_RaisesEventAndStampsAudit()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time);
        link.ClearDomainEvents();
        var actor = Guid.NewGuid();

        link.Revoke(actor, "Compromised", time);

        link.RevokedAt.ShouldBe(Now);
        link.RevokedBy.ShouldBe(actor);
        link.RevocationReason.ShouldBe("Compromised");
        DocumentPublicLinkRevokedEvent evt = link.DomainEvents.OfType<DocumentPublicLinkRevokedEvent>().Single();
        evt.RevokedBy.ShouldBe(actor);
        evt.Reason.ShouldBe("Compromised");
        link.IsActive(Now).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_TwiceThrows()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time);
        link.Revoke(null, null, time);
        Should.Throw<InvalidOperationException>(() => link.Revoke(null, null, time));
    }

    [Fact]
    public void RegisterConsumption_IncrementsAndRaisesEvent()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time, maxUses: 3);
        link.ClearDomainEvents();

        link.RegisterConsumption(time);

        link.CurrentUses.ShouldBe(1);
        link.DomainEvents.OfType<DocumentPublicLinkConsumedEvent>().Single().CurrentUses.ShouldBe(1);
    }

    [Fact]
    public void RegisterConsumption_AfterExpiryThrows()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time, ttl: TimeSpan.FromMinutes(5));
        time.Advance(TimeSpan.FromMinutes(6));
        Should.Throw<InvalidOperationException>(() => link.RegisterConsumption(time));
    }

    [Fact]
    public void RegisterConsumption_BeyondMaxUsesThrows()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time, maxUses: 1);
        link.RegisterConsumption(time);
        Should.Throw<InvalidOperationException>(() => link.RegisterConsumption(time));
    }

    [Fact]
    public void RegisterConsumption_AfterRevokeThrows()
    {
        FakeTimeProvider time = Time();
        DocumentPublicLink link = NewActive(time);
        link.Revoke(null, null, time);
        Should.Throw<InvalidOperationException>(() => link.RegisterConsumption(time));
    }
}
