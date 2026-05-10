using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed tests for <see cref="TenantQuotaService"/>. Verifies the lazy-create
/// path, atomic increment / decrement, and the clamp-at-zero behaviour on decrement.
/// </summary>
public sealed class TenantQuotaServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestFactory _factory = null!;
    private TenantQuotaService _sut = null!;
    private IClock _clock = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestFactory(options);

        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(Now);

        IOptions<GranitDocumentsOptions> opts = Microsoft.Extensions.Options.Options.Create(new GranitDocumentsOptions
        {
            DefaultTenantQuotaBytes = 5L * 1024L * 1024L * 1024L,
        });

        _sut = new TenantQuotaService(_factory, guidGen, _clock, opts);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EnsureTenantQuotaAsync_CreatesRowOnFirstCall_ReturnsExistingOnSecond()
    {
        TenantStorageQuota first = await _sut.EnsureTenantQuotaAsync(
            TenantId, TestContext.Current.CancellationToken);
        TenantStorageQuota second = await _sut.EnsureTenantQuotaAsync(
            TenantId, TestContext.Current.CancellationToken);

        first.Id.ShouldBe(second.Id);
        first.LimitBytes.ShouldBe(5L * 1024L * 1024L * 1024L);
        first.UsageBytes.ShouldBe(0);
    }

    [Fact]
    public async Task IncrementAsync_LazyCreatesAndAccumulates()
    {
        await _sut.IncrementAsync(TenantId, 4096, TestContext.Current.CancellationToken);
        await _sut.IncrementAsync(TenantId, 2048, TestContext.Current.CancellationToken);

        TenantStorageQuota? quota = await _sut.GetAsync(
            TenantId, TestContext.Current.CancellationToken);
        quota.ShouldNotBeNull();
        quota.UsageBytes.ShouldBe(6144);
    }

    [Fact]
    public async Task DecrementAsync_ClampsAtZero()
    {
        await _sut.IncrementAsync(TenantId, 100, TestContext.Current.CancellationToken);
        await _sut.DecrementAsync(TenantId, 500, TestContext.Current.CancellationToken);

        TenantStorageQuota? quota = await _sut.GetAsync(
            TenantId, TestContext.Current.CancellationToken);
        quota.ShouldNotBeNull();
        quota.UsageBytes.ShouldBe(0);
    }

    [Fact]
    public async Task DecrementAsync_NoRow_IsNoOp()
    {
        // Should not throw; just match zero rows and exit cleanly.
        await _sut.DecrementAsync(TenantId, 100, TestContext.Current.CancellationToken);

        TenantStorageQuota? quota = await _sut.GetAsync(
            TenantId, TestContext.Current.CancellationToken);
        quota.ShouldBeNull();
    }

    [Fact]
    public async Task IncrementAsync_NegativeDelta_Throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            _sut.IncrementAsync(TenantId, -1, TestContext.Current.CancellationToken));

    [Fact]
    public async Task GetAsync_NoRow_ReturnsNull()
    {
        TenantStorageQuota? result = await _sut.GetAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    private sealed class TestFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }
}
