using Granit.Documents.PublicLinks.BackgroundJobs.Jobs;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Documents.PublicLinks.Options;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.BackgroundJobs.Tests;

/// <summary>
/// Tests <see cref="PruneExpiredPublicLinksHandler"/>.
/// </summary>
/// <remarks>
/// Disabled-state short-circuit is exercised end-to-end against an in-memory
/// SQLite DbContext (no LINQ-translation cost). The deletion-path proper is
/// verified by the PostgreSQL integration shard
/// (Granit.Documents.PublicLinks.EntityFrameworkCore.Tests.Integration), which
/// is the only provider that ships full <c>DateTimeOffset</c> comparison
/// translation in EF Core 10.
/// </remarks>
public sealed class PruneExpiredPublicLinksHandlerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
    private SqliteConnection _connection = null!;
    private IDbContextFactory<DocumentsPublicLinksDbContext> _factory = null!;
    private IServiceProvider _sp = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<DocumentsPublicLinksDbContext> options =
            new DbContextOptionsBuilder<DocumentsPublicLinksDbContext>()
                .UseSqlite(_connection)
                .Options;

        await using DocumentsPublicLinksDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new SingletonFactory(options);
        ServiceCollection services = new();
        services.AddSingleton(_factory);
        _sp = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenDisabled()
    {
        // Seed a row that would be deleted if the job ran — confirms the
        // disabled branch short-circuits before any database access.
        DocumentPublicLink old = NewLink(expiresAt: Now.AddDays(-200));
        await SeedAsync(old);

        await PruneExpiredPublicLinksHandler.HandleAsync(
            new PruneExpiredPublicLinksJob(),
            _sp,
            new FixedClock(Now),
            BuildOptions(new PruningOptions { Enabled = false }),
            NullLogger<PruneExpiredPublicLinksJob>.Instance,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Guid> remaining = await ListAllIdsAsync();
        remaining.ShouldContain(old.Id);
    }

    private static DocumentPublicLink NewLink(DateTimeOffset expiresAt)
    {
        var link = DocumentPublicLink.Create(
            Guid.NewGuid(),
            documentId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            tokenHash: RandomHash(),
            scope: PublicLinkScope.Download,
            expiresAt: Now.AddYears(1),
            maxUses: null,
            timeProvider: new FixedTime(Now.AddYears(-1)));
        typeof(DocumentPublicLink)
            .GetProperty(nameof(DocumentPublicLink.ExpiresAt))!
            .SetValue(link, expiresAt);
        return link;
    }

    private static byte[] RandomHash()
    {
        byte[] h = new byte[32];
        Random.Shared.NextBytes(h);
        return h;
    }

    private async Task SeedAsync(params DocumentPublicLink[] links)
    {
        await using DocumentsPublicLinksDbContext context = await _factory.CreateDbContextAsync();
        context.DocumentPublicLinks.AddRange(links);
        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Guid>> ListAllIdsAsync()
    {
        await using DocumentsPublicLinksDbContext context = await _factory.CreateDbContextAsync();
        return await context.DocumentPublicLinks
            .Select(l => l.Id)
            .ToListAsync();
    }

    private static IOptionsMonitor<GranitDocumentsPublicLinksOptions> BuildOptions(PruningOptions pruning)
    {
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> monitor =
            Substitute.For<IOptionsMonitor<GranitDocumentsPublicLinksOptions>>();
        monitor.CurrentValue.Returns(new GranitDocumentsPublicLinksOptions { Pruning = pruning });
        return monitor;
    }

    private sealed class SingletonFactory(DbContextOptions<DocumentsPublicLinksDbContext> options)
        : IDbContextFactory<DocumentsPublicLinksDbContext>
    {
        public DocumentsPublicLinksDbContext CreateDbContext() => new(options);
        public Task<DocumentsPublicLinksDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentsPublicLinksDbContext(options));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; } = now;
        public bool SupportsMultipleTimezone => false;
        public DateTimeOffset Normalize(DateTimeOffset dateTime) => dateTime.ToUniversalTime();
        public DateTimeOffset ConvertToUserTime(DateTimeOffset utcDateTime) => utcDateTime;
        public DateTimeOffset ConvertToUtc(DateTimeOffset dateTime) => dateTime.ToUniversalTime();
    }
}
