using Granit.Encryption.EntityFrameworkCore;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.Encryption.ReEncryption;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.ReEncryption.Tests;

public sealed class DefaultReEncryptionJobTests
{
    private readonly IStringEncryptionService _encryption = Substitute.For<IStringEncryptionService>();

    public DefaultReEncryptionJobTests()
    {
        // Each encrypt call produces a ciphertext with a simple prefix so tests can verify re-encryption.
        _encryption.Encrypt(Arg.Any<string>()).Returns(ci => $"ENC:{ci.Arg<string>()}");
        _encryption.Decrypt(Arg.Any<string>()).Returns(ci =>
        {
            string s = ci.Arg<string>();
            return s.StartsWith("ENC:", StringComparison.Ordinal) ? s["ENC:".Length..] : null;
        });
    }

    [Fact]
    public async Task ReEncryptAsync_MarksEncryptedProperties_AsModified_AndSaves()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        // Seed one row
        using (TestDbContext ctx = new(options, _encryption))
        {
            ctx.Database.EnsureCreated();
            ctx.Patients.Add(new PatientEntity { Id = 1, Ssn = "123-45-6789", Name = "Alice" });
            ctx.SaveChanges();
        }

        // Verify the raw value is encrypted after initial save
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Ssn FROM Patients WHERE Id = 1";
        string? initialRaw = cmd.ExecuteScalar() as string;
        initialRaw.ShouldBe("ENC:123-45-6789");

        // Run re-encryption job
        IDbContextFactory<TestDbContext> factory = new InlineDbContextFactory(options, _encryption);
        DefaultReEncryptionJob<TestDbContext> sut = new(factory);

        await sut.ReEncryptAsync<PatientEntity>(cancellationToken: TestContext.Current.CancellationToken);

        // After re-encryption, the raw value should still be encrypted
        // (decrypt → re-encrypt cycle: "ENC:123-45-6789" → "123-45-6789" → "ENC:123-45-6789")
        string? reEncryptedRaw = cmd.ExecuteScalar() as string;
        reEncryptedRaw.ShouldBe("ENC:123-45-6789");
    }

    [Fact]
    public async Task ReEncryptAsync_DoesNotTouch_NonAnnotatedProperties()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using (TestDbContext ctx = new(options, _encryption))
        {
            ctx.Database.EnsureCreated();
            ctx.Patients.Add(new PatientEntity { Id = 1, Ssn = "123-45-6789", Name = "Alice" });
            ctx.SaveChanges();
        }

        _encryption.ClearReceivedCalls();

        IDbContextFactory<TestDbContext> factory = new InlineDbContextFactory(options, _encryption);
        DefaultReEncryptionJob<TestDbContext> sut = new(factory);

        await sut.ReEncryptAsync<PatientEntity>(cancellationToken: TestContext.Current.CancellationToken);

        // Name is not [Encrypted] — Encrypt must not be called with "Alice"
        _encryption.DidNotReceive().Encrypt("Alice");
    }

    [Fact]
    public async Task ReEncryptAsync_SkipsEntities_WhenNoEncryptedProperties()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using (TestDbContext ctx = new(options, _encryption))
        {
            ctx.Database.EnsureCreated();
        }

        _encryption.ClearReceivedCalls();

        IDbContextFactory<TestDbContext> factory = new InlineDbContextFactory(options, _encryption);
        DefaultReEncryptionJob<TestDbContext> sut = new(factory);

        // PlainEntity has no [Encrypted] properties — should complete without calling Encrypt
        await sut.ReEncryptAsync<PlainEntity>(cancellationToken: TestContext.Current.CancellationToken);

        _encryption.DidNotReceiveWithAnyArgs().Encrypt(default!);
    }

    [Fact]
    public async Task ReEncryptAsync_ProcessesMultipleBatches_AllEntitiesReadable()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using (TestDbContext ctx = new(options, _encryption))
        {
            ctx.Database.EnsureCreated();
            for (int i = 1; i <= 5; i++)
            {
                ctx.Patients.Add(new PatientEntity { Id = i, Ssn = $"SSN-{i}", Name = $"Patient{i}" });
            }

            ctx.SaveChanges();
        }

        IDbContextFactory<TestDbContext> factory = new InlineDbContextFactory(options, _encryption);
        DefaultReEncryptionJob<TestDbContext> sut = new(factory);

        // batchSize=2 forces multiple iterations (5 rows / 2 = 3 batches: 2+2+1)
        await sut.ReEncryptAsync<PatientEntity>(batchSize: 2, cancellationToken: TestContext.Current.CancellationToken);

        // After re-encryption, all entities must still be readable with their original SSNs
        using TestDbContext verify = new(options, _encryption);
        var all = verify.Patients.OrderBy(p => p.Id).ToList();

        all.Count.ShouldBe(5);
        for (int i = 0; i < 5; i++)
        {
            all[i].Ssn.ShouldBe($"SSN-{i + 1}");
        }
    }

    // --- Test fixtures ---

    private sealed class PatientEntity
    {
        public int Id { get; set; }

        [Encrypted]
        public string Ssn { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class PlainEntity
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options,
        IStringEncryptionService encryptionService) : DbContext(options)
    {
        public DbSet<PatientEntity> Patients => Set<PatientEntity>();
        public DbSet<PlainEntity> Plains => Set<PlainEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyEncryptionConventions(encryptionService);
        }
    }

    private sealed class InlineDbContextFactory(
        DbContextOptions<TestDbContext> dbOptions,
        IStringEncryptionService encryptionService) : IDbContextFactory<TestDbContext>
    {
        public TestDbContext CreateDbContext() => new(dbOptions, encryptionService);
    }
}
