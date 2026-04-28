using System.Data.Common;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.EntityFrameworkCore.Internal;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres integration tests for <see cref="BalanceAccountPartyReferenceRewriter"/>.
/// <c>ExecuteUpdateAsync</c> requires a relational provider, so the EF in-memory
/// provider isn't a substitute.
/// </summary>
public sealed class BalanceAccountPartyReferenceRewriterPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestCustomerBalanceDbContextFactory _factory = null!;
    private BalanceAccountPartyReferenceRewriter _sut = null!;

    public BalanceAccountPartyReferenceRewriterPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _factory = new TestCustomerBalanceDbContextFactory(_postgres.ConnectionString);
        await using CustomerBalanceDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        string p = GranitCustomerBalanceDbProperties.DbTablePrefix;
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {p}transactions, {p}accounts RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);

        _sut = new BalanceAccountPartyReferenceRewriter(_factory);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task RewriteAsync_RedirectsLoserAccountsOntoSurvivor()
    {
        var survivorId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();

        // Distinct currencies on loser to avoid the per-currency uniqueness collision noted on
        // the rewriter (cardinality caveat — same-currency same-survivor is documented as
        // domain-level tech-debt for v2).
        await SeedAsync(
            NewAccount(loserId, "EUR"),
            NewAccount(loserId, "USD"),
            NewAccount(survivorId, "GBP"),
            NewAccount(otherPartyId, "EUR"));

        int rewritten = await _sut.RewriteAsync(survivorId, loserId, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(2);

        await using CustomerBalanceDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var survivorPartyId = PartyId.Create(survivorId);
        int survivorCount = await db.Accounts
            .CountAsync(a => a.PartyId == survivorPartyId, TestContext.Current.CancellationToken);
        survivorCount.ShouldBe(3);

        var loserPartyId = PartyId.Create(loserId);
        int loserCount = await db.Accounts
            .CountAsync(a => a.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        loserCount.ShouldBe(0);

        var otherPartyIdValueObject = PartyId.Create(otherPartyId);
        int otherCount = await db.Accounts
            .CountAsync(a => a.PartyId == otherPartyIdValueObject, TestContext.Current.CancellationToken);
        otherCount.ShouldBe(1);
    }

    [Fact]
    public async Task RewriteAsync_SurfacesUniqueConstraint_OnSameCurrencyCollision()
    {
        // Documents the v1 behaviour: when both parties have an account in the same currency,
        // the per-(PartyId, Currency) unique index fires post-rewrite and the orchestrator's
        // TransactionScope rolls back. Real consolidation is tech-debt #1402.
        var survivorId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        await SeedAsync(NewAccount(survivorId, "EUR"), NewAccount(loserId, "EUR"));

        // ExecuteUpdateAsync bypasses the change tracker, so the provider surfaces the unique
        // constraint violation directly as a DbException — Npgsql.PostgresException
        // (SqlState 23505) on PostgreSQL, Microsoft.Data.SqlClient.SqlException
        // (Number 2627 / 2601) on SQL Server. Asserting on the common DbException base type
        // keeps the test provider-agnostic; the contract we document is "the database
        // surfaces an error and the orchestrator's TransactionScope rolls back", not the
        // specific SqlState code.
        await Should.ThrowAsync<DbException>(() =>
            _sut.RewriteAsync(survivorId, loserId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RewriteAsync_ReturnsZero_WhenLoserHasNoAccounts()
    {
        int rewritten = await _sut.RewriteAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);
        rewritten.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_ReturnsLoserAccountCount_WithoutMutating()
    {
        var loserId = Guid.NewGuid();
        await SeedAsync(NewAccount(loserId, "EUR"), NewAccount(loserId, "USD"));

        int count = await _sut.CountAsync(Guid.NewGuid(), loserId, TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        await using CustomerBalanceDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var loserPartyId = PartyId.Create(loserId);
        int still = await db.Accounts
            .CountAsync(a => a.PartyId == loserPartyId, TestContext.Current.CancellationToken);
        still.ShouldBe(2);
    }

    private async Task SeedAsync(params BalanceAccount[] accounts)
    {
        await using CustomerBalanceDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static BalanceAccount NewAccount(Guid partyId, string currency) =>
        BalanceAccount.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            partyId: PartyId.Create(partyId),
            currency: currency);
}
