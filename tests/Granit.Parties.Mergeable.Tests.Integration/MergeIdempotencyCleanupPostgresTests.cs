using Granit.Mergeable.BackgroundJobs.Services;
using Granit.Mergeable.EntityFrameworkCore;
using Granit.Mergeable.EntityFrameworkCore.Domain;
using Granit.Mergeable.EntityFrameworkCore.Internal;
using Granit.Mergeable.EntityFrameworkCore.Options;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Postgres integration test for <see cref="MergeIdempotencyCleanupService"/>. Lives in the
/// integration suite because <c>ExecuteDeleteAsync</c> on a <c>DateTimeOffset</c> column is
/// not supported by SQLite or EF in-memory — the production query shape only translates
/// natively under Npgsql.
/// </summary>
public sealed class MergeIdempotencyCleanupPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private TestMergeableDbContextFactory _factory = null!;

    public MergeIdempotencyCleanupPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _factory = new TestMergeableDbContextFactory(_postgres.ConnectionString);

        await using MergeableDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE SCHEMA IF NOT EXISTS granit;
            CREATE TABLE IF NOT EXISTS granit.merge_idempotency (
                "Id" uuid NOT NULL,
                "TenantId" uuid NULL,
                "Key" character varying(128) NOT NULL,
                "RequestHash" character varying(64) NOT NULL,
                "SurvivorId" uuid NOT NULL,
                "LoserId" uuid NOT NULL,
                "ResultJson" text NOT NULL,
                "ResultMac" character varying(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_merge_idempotency" PRIMARY KEY ("Id")
            );
            """,
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE granit.merge_idempotency RESTART IDENTITY;",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task ExecuteAsync_DeletesOnlyRowsOlderThanRetention()
    {
        await SeedAsync(
            entry("recent", Now - TimeSpan.FromHours(1)),
            entry("on-the-edge", Now - TimeSpan.FromHours(23) - TimeSpan.FromMinutes(59)),
            entry("expired-1", Now - TimeSpan.FromHours(25)),
            entry("expired-2", Now - TimeSpan.FromDays(7)));

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var sut = new MergeIdempotencyCleanupService(
            _factory,
            Options.Create(new MergeableOptions { IdempotencyRetention = TimeSpan.FromHours(24) }),
            clock,
            NullLogger<MergeIdempotencyCleanupService>.Instance);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await using MergeableDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<string> remaining = await db.MergeIdempotencyEntries
            .Select(e => e.Key)
            .OrderBy(k => k)
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(["on-the-edge", "recent"]);
    }

    [Fact]
    public async Task ExecuteAsync_OnEmptyTable_IsIdempotent()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var sut = new MergeIdempotencyCleanupService(
            _factory,
            Options.Create(new MergeableOptions()),
            clock,
            NullLogger<MergeIdempotencyCleanupService>.Instance);

        // Two consecutive runs on an empty table — no throw, no side effect (idempotent).
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCustomRetentionWindow()
    {
        await SeedAsync(
            entry("just-now", Now - TimeSpan.FromMinutes(30)),
            entry("two-hours-old", Now - TimeSpan.FromHours(2)));

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var sut = new MergeIdempotencyCleanupService(
            _factory,
            Options.Create(new MergeableOptions { IdempotencyRetention = TimeSpan.FromHours(1) }),
            clock,
            NullLogger<MergeIdempotencyCleanupService>.Instance);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await using MergeableDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<string> remaining = await db.MergeIdempotencyEntries
            .Select(e => e.Key)
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.ShouldBe(["just-now"]);
    }

    private static MergeIdempotencyEntry entry(string key, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = null,
        Key = key,
        RequestHash = new string('a', 64),
        SurvivorId = Guid.NewGuid(),
        LoserId = Guid.NewGuid(),
        ResultJson = "{}",
        ResultMac = new string('b', 64),
        CreatedAt = createdAt,
    };

    private async Task SeedAsync(params MergeIdempotencyEntry[] entries)
    {
        await using MergeableDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.MergeIdempotencyEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
