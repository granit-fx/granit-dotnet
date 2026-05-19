using System.Security.Cryptography;
using Granit.Encryption.EntityFrameworkCore.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class EncryptionIsolationMaterializationInterceptorTests : IDisposable
{
    private readonly IEntityEncryptionKeyStore _keyStore = Substitute.For<IEntityEncryptionKeyStore>();
    private readonly EncryptionIsolationSaveChangesInterceptor _saveInterceptor;
    private readonly EncryptionIsolationMaterializationInterceptor _readInterceptor;
    private readonly SqliteConnection _connection;
    private readonly byte[] _testKey;

    public EncryptionIsolationMaterializationInterceptorTests()
    {
        _testKey = RandomNumberGenerator.GetBytes(32);

        _saveInterceptor = new EncryptionIsolationSaveChangesInterceptor(
            _keyStore,
            NullLogger<EncryptionIsolationSaveChangesInterceptor>.Instance);

        _readInterceptor = new EncryptionIsolationMaterializationInterceptor(
            _keyStore,
            NullLogger<EncryptionIsolationMaterializationInterceptor>.Instance);

        _keyStore.GetOrCreateKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_testKey);

        _keyStore.GetKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_testKey);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task RoundTrip_SaveAndRead_ReturnsOriginalPlaintext()
    {
        var entityId = Guid.NewGuid();

        // Save with encryption
        await using (TestDbContext writeCtx = CreateContext())
        {
            await writeCtx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            writeCtx.Patients.Add(new PatientEntity
            {
                Id = entityId,
                Name = "Alice",
                Ssn = "123-45-6789"
            });
            await writeCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read with decryption
        await using (TestDbContext readCtx = CreateContext())
        {
            PatientEntity? patient = await readCtx.Patients.FindAsync([entityId], TestContext.Current.CancellationToken);

            patient.ShouldNotBeNull();
            patient.Ssn.ShouldBe("123-45-6789");
            patient.Name.ShouldBe("Alice");
        }
    }

    [Fact]
    public async Task Read_AfterKeyShredded_ReturnsNullForNullableProperty()
    {
        var entityId = Guid.NewGuid();

        // Save with encryption
        await using (TestDbContext writeCtx = CreateContext())
        {
            await writeCtx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            writeCtx.Patients.Add(new PatientEntity
            {
                Id = entityId,
                Name = "Bob",
                Ssn = "987-65-4321"
            });
            await writeCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Simulate crypto-shredding: key store returns null
        _keyStore.GetKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        // Read after shredding
        await using (TestDbContext readCtx = CreateContext())
        {
            PatientEntity? patient = await readCtx.Patients.FindAsync([entityId], TestContext.Current.CancellationToken);

            patient.ShouldNotBeNull();
            patient.Ssn.ShouldBeNull("Shredded entity should have null isolated properties");
            patient.Name.ShouldBe("Bob", "Non-isolated properties should be unaffected");
        }
    }

    [Fact]
    public async Task Read_WithNullStoredValue_ReturnsNull()
    {
        var entityId = Guid.NewGuid();

        // Insert directly with NULL Ssn
        await using (TestDbContext writeCtx = CreateContext())
        {
            await writeCtx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            writeCtx.Patients.Add(new PatientEntity
            {
                Id = entityId,
                Name = "Charlie",
                Ssn = null
            });
            await writeCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read — null should stay null (no decryption attempt)
        await using (TestDbContext readCtx = CreateContext())
        {
            PatientEntity? patient = await readCtx.Patients.FindAsync([entityId], TestContext.Current.CancellationToken);

            patient.ShouldNotBeNull();
            patient.Ssn.ShouldBeNull();
        }
    }

    private TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_saveInterceptor, _readInterceptor)
            .Options;

        return new TestDbContext(options);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<PatientEntity> Patients => Set<PatientEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PatientEntity>(b =>
            {
                b.ToTable("Patients");
                b.HasKey(e => e.Id);
            });
        }
    }

    private sealed class PatientEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [Encrypted(KeyIsolation = true)]
        public string? Ssn { get; set; }
    }
}
