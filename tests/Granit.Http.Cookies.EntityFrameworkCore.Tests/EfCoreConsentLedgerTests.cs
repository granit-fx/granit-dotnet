using Granit.Http.Cookies.EntityFrameworkCore.Internal;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test the internal DbContext and services

namespace Granit.Http.Cookies.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite-backed tests for <see cref="EfCoreConsentLedger"/>: append-only persistence
/// (including the delimited-string category round-trip) and the after-save
/// <see cref="ConsentRecordedEto"/> dispatch contract.
/// </summary>
public sealed class EfCoreConsentLedgerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset DecidedAt = new(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CookiesDbContext> _dbOptions;
    private readonly RecordingIntegrationEventDispatcher _dispatcher = new();
    private readonly EfCoreConsentLedger _ledger;

    public EfCoreConsentLedgerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CookiesDbContext>()
            .UseSqlite(_connection)
            .Options;

        using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        context.Database.EnsureCreated();

        _ledger = new EfCoreConsentLedger(new StubCookiesDbContextFactory(_dbOptions), _dispatcher);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task RecordAsync_PersistsRow_AndRoundtripsCategories()
    {
        // Arrange
        CookieConsentRecord record = CreateRecord();

        // Act
        await _ledger.RecordAsync(record, TestContext.Current.CancellationToken);

        // Assert — read through a fresh context so values come from SQLite, not the tracker.
        await using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        CookieConsentRecord persisted = (await context.ConsentRecords.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        persisted.Id.ShouldNotBe(Guid.Empty);
        persisted.GrantedCategories.ShouldBe(["strictly_necessary", "analytics"]);
        persisted.DeniedCategories.ShouldBe(["marketing"]);
        persisted.Mode.ShouldBe(CookieConsentMode.OptIn);
        persisted.CmpSource.ShouldBe("cookieconsent");
        persisted.AnonymizedIp.ShouldBe("203.0.113.0");
        persisted.UserAgent.ShouldBe("Mozilla/5.0 (X11; Linux x86_64)");
        persisted.CorrelationId.ShouldBe("00-trace-id-01");
        persisted.DecidedAt.ShouldBe(DecidedAt);
    }

    [Fact]
    public async Task RecordAsync_PersistsEmptyCategoryList_AsEmpty()
    {
        // Arrange — deny-all decision: granted list is empty.
        var record = CookieConsentRecord.Create(
            [], ["analytics", "marketing"], CookieConsentMode.OptOut, "cmp", DecidedAt);

        // Act
        await _ledger.RecordAsync(record, TestContext.Current.CancellationToken);

        // Assert
        await using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        CookieConsentRecord persisted = (await context.ConsentRecords.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        persisted.GrantedCategories.ShouldBeEmpty();
        persisted.DeniedCategories.ShouldBe(["analytics", "marketing"]);
    }

    [Fact]
    public async Task RecordAsync_IsAppendOnly_EachDecisionAddsARow()
    {
        // Act — the same user changing their mind appends, never updates.
        await _ledger.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);
        await _ledger.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);

        // Assert
        await using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        (await context.ConsentRecords.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task RecordAsync_DispatchesConsentRecordedEto_WithPersistedValues()
    {
        // Arrange
        CookieConsentRecord record = CreateRecord();

        // Act
        await _ledger.RecordAsync(record, TestContext.Current.CancellationToken);

        // Assert
        ConsentRecordedEto eto = _dispatcher.Dispatched.ShouldHaveSingleItem()
            .ShouldBeOfType<ConsentRecordedEto>();
        eto.RecordId.ShouldBe(record.Id);
        eto.RecordId.ShouldNotBe(Guid.Empty);
        eto.GrantedCategories.ShouldBe(["strictly_necessary", "analytics"]);
        eto.DeniedCategories.ShouldBe(["marketing"]);
        eto.Mode.ShouldBe("OptIn");
        eto.DecidedAt.ShouldBe(DecidedAt);
    }

    [Fact]
    public async Task RecordAsync_WhenSaveFails_DispatchesNoEvent()
    {
        // Arrange — SQLite does not enforce varchar lengths, so force the failure
        // via a duplicate primary key instead.
        CookieConsentRecord first = CreateRecord();
        await _ledger.RecordAsync(first, TestContext.Current.CancellationToken);
        _dispatcher.Dispatched.Clear();

        CookieConsentRecord duplicate = CreateRecord();
        duplicate.Id = first.Id;

        // Act
        await Should.ThrowAsync<DbUpdateException>(
            () => _ledger.RecordAsync(duplicate, TestContext.Current.CancellationToken));

        // Assert — never an event for a row that failed to persist.
        _dispatcher.Dispatched.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordAsync_WithNullRecord_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => _ledger.RecordAsync(null!, TestContext.Current.CancellationToken));

    private static CookieConsentRecord CreateRecord() => CookieConsentRecord.Create(
        ["strictly_necessary", "analytics"],
        ["marketing"],
        CookieConsentMode.OptIn,
        "cookieconsent",
        DecidedAt,
        anonymizedIp: "203.0.113.0",
        userAgent: "Mozilla/5.0 (X11; Linux x86_64)",
        correlationId: "00-trace-id-01");

    private sealed class RecordingIntegrationEventDispatcher : IIntegrationEventDispatcher
    {
        public List<IIntegrationEvent> Dispatched { get; } = [];

        public Task DispatchAsync(
            IReadOnlyList<IIntegrationEvent> integrationEvents,
            CancellationToken cancellationToken = default)
        {
            Dispatched.AddRange(integrationEvents);
            return Task.CompletedTask;
        }
    }
}
