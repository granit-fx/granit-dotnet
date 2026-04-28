using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres integration tests for <see cref="SubscriptionPartyReferenceRewriter"/>.
/// <c>ExecuteUpdateAsync</c> requires a relational provider, so the EF in-memory
/// provider isn't a substitute.
/// </summary>
public sealed class SubscriptionPartyReferenceRewriterPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestSubscriptionsDbContextFactory _factory = null!;
    private SubscriptionPartyReferenceRewriter _sut = null!;

    public SubscriptionPartyReferenceRewriterPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _factory = new TestSubscriptionsDbContextFactory(_postgres.ConnectionString);
        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Truncate every Subscriptions table so each test starts clean. The schema is
        // discovered through GranitSubscriptionsDbProperties so the test stays aligned with
        // a future prefix change.
        string p = GranitSubscriptionsDbProperties.DbTablePrefix;
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE "
            + $"{p}subscription_external_mappings, {p}subscription_phases, {p}subscription_seats, {p}subscriptions, "
            + $"{p}plan_external_mappings, {p}plan_feature_values, {p}pricing_tiers, {p}plan_prices, {p}plans "
            + "RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);

        _sut = new SubscriptionPartyReferenceRewriter(_factory);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task RewriteAsync_RedirectsLoserSubscriptionsOntoSurvivor()
    {
        var survivorId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();

        await SeedAsync(
            NewSubscription(loserId),
            NewSubscription(loserId),
            NewSubscription(survivorId),
            NewSubscription(otherPartyId));

        int rewritten = await _sut.RewriteAsync(survivorId, loserId, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(2);

        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var survivorPartyId = PartyId.Create(survivorId);
        int survivorCount = await db.Subscriptions
            .CountAsync(s => s.PartyId == survivorPartyId, TestContext.Current.CancellationToken);
        survivorCount.ShouldBe(3);

        var loserPartyId = PartyId.Create(loserId);
        int loserCount = await db.Subscriptions
            .CountAsync(s => s.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        loserCount.ShouldBe(0);

        var otherPartyIdValueObject = PartyId.Create(otherPartyId);
        int otherCount = await db.Subscriptions
            .CountAsync(s => s.PartyId == otherPartyIdValueObject, TestContext.Current.CancellationToken);
        otherCount.ShouldBe(1);
    }

    [Fact]
    public async Task RewriteAsync_ReturnsZero_WhenLoserHasNoSubscriptions()
    {
        int rewritten = await _sut.RewriteAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);
        rewritten.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_ReturnsLoserSubscriptionCount_WithoutMutating()
    {
        var loserId = Guid.NewGuid();
        await SeedAsync(NewSubscription(loserId), NewSubscription(loserId));

        int count = await _sut.CountAsync(Guid.NewGuid(), loserId, TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var loserPartyId = PartyId.Create(loserId);
        int still = await db.Subscriptions
            .CountAsync(s => s.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        still.ShouldBe(2);
    }

    private async Task SeedAsync(params Subscription[] subscriptions)
    {
        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Subscriptions.AddRange(subscriptions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Subscription NewSubscription(Guid partyId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            id: SubscriptionId.Create(Guid.NewGuid()),
            tenantId: Guid.NewGuid(),
            partyId: PartyId.Create(partyId),
            planId: PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            period: new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));
    }
}
