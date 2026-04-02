using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingCleanerTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuditingDbContext> _dbOptions;

    public EfCoreAuditingCleanerTests()
    {
        // SQLite in-memory with a shared connection — supports ExecuteUpdateAsync.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new AuditingDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task PseudonymizeByUserAsync_ReplacesPersonalData()
    {
        // Seed two entries for the target user and one for another user.
        await using (AuditingDbContext seedCtx = new(_dbOptions))
        {
            seedCtx.AuditEntries.AddRange(
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    UserId = "user-to-erase",
                    UserName = "John Doe",
                    IpAddress = "192.168.1.1",
                    UserAgent = "Mozilla/5.0",
                    Category = AuditCategory.DataMutation,
                },
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    UserId = "user-to-erase",
                    UserName = "John Doe",
                    IpAddress = "10.0.0.1",
                    UserAgent = "curl/8.0",
                    Category = AuditCategory.ConfigurationChange,
                },
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    UserId = "other-user",
                    UserName = "Jane Smith",
                    IpAddress = "172.16.0.1",
                    UserAgent = "Chrome/120",
                    Category = AuditCategory.DataMutation,
                });
            await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingCleaner cleaner = new(factory, NullLogger<EfCoreAuditingCleaner>.Instance);

        int count = await cleaner.PseudonymizeByUserAsync(
            "user-to-erase", TestContext.Current.CancellationToken);

        count.ShouldBe(2);

        // Verify pseudonymized entries.
        await using AuditingDbContext verifyCtx = new(_dbOptions);
        string expectedHash = EfCoreAuditingCleaner.HashUserId("user-to-erase");

        List<AuditEntry> pseudonymized = await verifyCtx.AuditEntries
            .Where(e => e.UserId == expectedHash)
            .ToListAsync(TestContext.Current.CancellationToken);

        pseudonymized.Count.ShouldBe(2);
        foreach (AuditEntry entry in pseudonymized)
        {
            entry.UserName.ShouldBe("[pseudonymized]");
            entry.IpAddress.ShouldBeNull();
            entry.UserAgent.ShouldBeNull();
        }

        // Verify unrelated user is untouched.
        AuditEntry? otherEntry = await verifyCtx.AuditEntries
            .FirstOrDefaultAsync(e => e.UserId == "other-user", TestContext.Current.CancellationToken);
        otherEntry.ShouldNotBeNull();
        otherEntry.UserName.ShouldBe("Jane Smith");
        otherEntry.IpAddress.ShouldBe("172.16.0.1");
        otherEntry.UserAgent.ShouldBe("Chrome/120");
    }

    [Fact]
    public async Task PseudonymizeByUserAsync_NoMatchingEntries_ReturnsZero()
    {
        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingCleaner cleaner = new(factory, NullLogger<EfCoreAuditingCleaner>.Instance);

        int count = await cleaner.PseudonymizeByUserAsync(
            "nonexistent-user", TestContext.Current.CancellationToken);

        count.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PseudonymizeByUserAsync_InvalidUserId_Throws(string? userId)
    {
        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingCleaner cleaner = new(factory, NullLogger<EfCoreAuditingCleaner>.Instance);

        await Should.ThrowAsync<ArgumentException>(() =>
            cleaner.PseudonymizeByUserAsync(userId!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HashUserId_ProducesDeterministicSha256()
    {
        string hash1 = EfCoreAuditingCleaner.HashUserId("user-42");
        string hash2 = EfCoreAuditingCleaner.HashUserId("user-42");

        hash1.ShouldBe(hash2);
        hash1.ShouldStartWith("sha256:");
        hash1.Length.ShouldBe("sha256:".Length + 64); // SHA-256 = 64 hex chars
    }

    [Fact]
    public void HashUserId_DifferentInputs_ProduceDifferentHashes()
    {
        string hash1 = EfCoreAuditingCleaner.HashUserId("user-1");
        string hash2 = EfCoreAuditingCleaner.HashUserId("user-2");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public async Task PseudonymizeByUserAsync_IsIdempotent()
    {
        await using (AuditingDbContext seedCtx = new(_dbOptions))
        {
            seedCtx.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                UserId = "user-to-erase",
                UserName = "John Doe",
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Category = AuditCategory.DataMutation,
            });
            await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingCleaner cleaner = new(factory, NullLogger<EfCoreAuditingCleaner>.Instance);

        int firstPass = await cleaner.PseudonymizeByUserAsync(
            "user-to-erase", TestContext.Current.CancellationToken);
        firstPass.ShouldBe(1);

        // Second call with same userId should find zero entries (already pseudonymized).
        int secondPass = await cleaner.PseudonymizeByUserAsync(
            "user-to-erase", TestContext.Current.CancellationToken);
        secondPass.ShouldBe(0);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options);
    }
}
