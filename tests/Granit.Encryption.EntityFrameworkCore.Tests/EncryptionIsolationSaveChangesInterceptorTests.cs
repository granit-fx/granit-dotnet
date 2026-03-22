using Granit.Encryption.EntityFrameworkCore.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class EncryptionIsolationSaveChangesInterceptorTests : IDisposable
{
    private readonly IEntityEncryptionKeyStore _keyStore = Substitute.For<IEntityEncryptionKeyStore>();
    private readonly EncryptionIsolationSaveChangesInterceptor _interceptor;
    private readonly SqliteConnection _connection;
    private readonly byte[] _testKey = new byte[32];

    public EncryptionIsolationSaveChangesInterceptorTests()
    {
        _interceptor = new EncryptionIsolationSaveChangesInterceptor(
            _keyStore,
            NullLogger<EncryptionIsolationSaveChangesInterceptor>.Instance);

        Random.Shared.NextBytes(_testKey);

        _keyStore.GetOrCreateKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_testKey);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task SavingChanges_EncryptsIsolatedProperties()
    {
        await using TestDbContext ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ctx.Patients.Add(new PatientEntity
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Ssn = "123-45-6789"
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Verify the key store was called
        await _keyStore.Received(1).GetOrCreateKeyAsync(
            "PatientEntity", Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Verify raw DB value is encrypted (not plaintext)
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Ssn FROM Patients WHERE Name = 'Alice'";
        string? rawValue = cmd.ExecuteScalar() as string;

        rawValue.ShouldNotBeNull();
        rawValue.ShouldNotBe("123-45-6789", "Isolated property should be encrypted in DB");
    }

    [Fact]
    public async Task SavingChanges_SkipsNonIsolatedProperties()
    {
        await using TestDbContext ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ctx.Patients.Add(new PatientEntity
        {
            Id = Guid.NewGuid(),
            Name = "Bob",
            Ssn = "987-65-4321"
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Name is not annotated with [Encrypted] — should be stored as-is
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Patients WHERE Name = 'Bob'";
        string? rawName = cmd.ExecuteScalar() as string;

        rawName.ShouldBe("Bob");
    }

    [Fact]
    public async Task SavingChanges_HandlesNullPropertyValue()
    {
        await using TestDbContext ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ctx.Patients.Add(new PatientEntity
        {
            Id = Guid.NewGuid(),
            Name = "Charlie",
            Ssn = null
        });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Ssn FROM Patients WHERE Name = 'Charlie'";
        object? rawValue = cmd.ExecuteScalar();

        rawValue.ShouldBe(DBNull.Value);
    }

    private TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
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
