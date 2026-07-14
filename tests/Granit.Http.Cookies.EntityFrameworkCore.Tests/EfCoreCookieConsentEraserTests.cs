using Granit.Http.Cookies.EntityFrameworkCore.Internal;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test the internal DbContext and services

namespace Granit.Http.Cookies.EntityFrameworkCore.Tests;

/// <summary>
/// GDPR Art. 17 hard-delete tests for <see cref="EfCoreCookieConsentEraser"/>: only the
/// subject's rows in the given tenant go; anonymous rows (no personal data) are retained.
/// </summary>
public sealed class EfCoreCookieConsentEraserTests : IAsyncDisposable
{
    private const string Alice = "user-alice";
    private const string Bob = "user-bob";

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CookiesDbContext> _dbOptions;

    public EfCoreCookieConsentEraserTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CookiesDbContext>()
            .UseSqlite(_connection)
            .Options;

        using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        context.Database.EnsureCreated();

        context.ConsentRecords.Add(CreateRecord(_tenantA, Alice));
        context.ConsentRecords.Add(CreateRecord(_tenantA, Alice));
        context.ConsentRecords.Add(CreateRecord(_tenantA, Bob));
        context.ConsentRecords.Add(CreateRecord(_tenantB, Alice));
        context.ConsentRecords.Add(CreateRecord(_tenantA, createdBy: string.Empty)); // anonymous CMP post
        context.SaveChanges();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EraseUserDataAsync_HardDeletesOnlyTheSubjectsRowsInTheTenant()
    {
        // Arrange — the eraser runs within the subject's tenant scope, like every
        // per-module personal-data eraser (multi-tenant filter applies).
        EfCoreCookieConsentEraser eraser = new(
            new StubCookiesDbContextFactory(_dbOptions, TenantContext(_tenantA)));

        // Act
        int affected = await eraser.EraseUserDataAsync(Alice, _tenantA, TestContext.Current.CancellationToken);

        // Assert — Alice/tenantA gone; Bob, Alice/tenantB, and the anonymous row remain.
        affected.ShouldBe(2);
        await using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        List<CookieConsentRecord> remaining = await context.ConsentRecords.AsNoTracking()
            .IgnoreQueryFilters() // assert across every partition, not just the ambient one
            .ToListAsync(TestContext.Current.CancellationToken);
        remaining.Count.ShouldBe(3);
        remaining.ShouldNotContain(r => r.CreatedBy == Alice && r.TenantId == _tenantA);
        remaining.ShouldContain(r => r.CreatedBy == Bob && r.TenantId == _tenantA);
        remaining.ShouldContain(r => r.CreatedBy == Alice && r.TenantId == _tenantB);
        remaining.ShouldContain(r => r.CreatedBy == string.Empty && r.TenantId == _tenantA);
    }

    [Fact]
    public async Task EraseUserDataAsync_UnknownUser_DeletesNothing()
    {
        // Arrange
        EfCoreCookieConsentEraser eraser = new(
            new StubCookiesDbContextFactory(_dbOptions, TenantContext(_tenantA)));

        // Act
        int affected = await eraser.EraseUserDataAsync("user-nobody", _tenantA, TestContext.Current.CancellationToken);

        // Assert
        affected.ShouldBe(0);
        await using CookiesDbContext context = new(_dbOptions, GranitDesignTime.CurrentTenant);
        (await context.ConsentRecords.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(5);
    }

    [Fact]
    public async Task EraseUserDataAsync_WithEmptyUserId_Throws()
    {
        // Arrange — an empty userId would match every anonymous row: reject it.
        EfCoreCookieConsentEraser eraser = new(new StubCookiesDbContextFactory(_dbOptions));

        // Act + Assert
        await Should.ThrowAsync<ArgumentException>(
            () => eraser.EraseUserDataAsync(string.Empty, _tenantA, TestContext.Current.CancellationToken));
    }

    private static ICurrentTenant TenantContext(Guid? tenantId)
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(tenantId is not null);
        currentTenant.Id.Returns(tenantId);
        return currentTenant;
    }

    private static CookieConsentRecord CreateRecord(Guid? tenantId, string createdBy)
    {
        CookieConsentRecord record = CookieConsentRecordTenantFilterTests.CreateRecord(tenantId, "cookieconsent");
        record.CreatedBy = createdBy;
        return record;
    }
}
