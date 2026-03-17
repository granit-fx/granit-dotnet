using Granit.Encryption.EntityFrameworkCore;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class ApplyEncryptionConventionsTests
{
    private readonly IStringEncryptionService _encryption = Substitute.For<IStringEncryptionService>();

    [Fact]
    public void ApplyEncryptionConventions_AppliesConverter_ToEncryptedStringProperties()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options, _encryption);

        IEntityType patientType = ctx.Model.FindEntityType(typeof(PatientEntity))!;
        IProperty ssnProperty = patientType.FindProperty(nameof(PatientEntity.Ssn))!;
        IProperty nameProperty = patientType.FindProperty(nameof(PatientEntity.Name))!;

        ssnProperty.GetValueConverter().ShouldBeOfType<EncryptedStringConverter>();
        nameProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void ApplyEncryptionConventions_DoesNotApplyConverter_ToNonAnnotatedProperties()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options, _encryption);

        IEntityType patientType = ctx.Model.FindEntityType(typeof(PatientEntity))!;
        IProperty nameProperty = patientType.FindProperty(nameof(PatientEntity.Name))!;

        nameProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void EncryptedAttribute_IsApplied_OnSave()
    {
        _encryption.Encrypt("123-45-6789").Returns("ENC:123-45-6789");
        _encryption.Decrypt("ENC:123-45-6789").Returns("123-45-6789");

        // SQLite applies value converters — InMemory provider does not.
        // Share a single open connection so all contexts see the same in-memory DB.
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

        // Verify the raw database value is encrypted (not the original plaintext).
        // We read raw SQL because EF Core would apply the decrypt converter on read.
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Ssn FROM Patients WHERE Id = 1";
        string? rawValue = cmd.ExecuteScalar() as string;
        rawValue.ShouldBe("ENC:123-45-6789");
    }

    [Fact]
    public void EncryptedAttribute_IsDecrypted_OnRead()
    {
        _encryption.Encrypt("123-45-6789").Returns("ENC:123-45-6789");
        _encryption.Decrypt("ENC:123-45-6789").Returns("123-45-6789");

        // SQLite applies value converters — InMemory provider does not.
        // Share a single open connection so all contexts see the same in-memory DB.
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

        using (TestDbContext ctx = new(options, _encryption))
        {
            PatientEntity patient = ctx.Patients.Find(1)!;
            patient.Ssn.ShouldBe("123-45-6789");
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

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options,
        IStringEncryptionService encryptionService) : DbContext(options)
    {
        public DbSet<PatientEntity> Patients => Set<PatientEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyEncryptionConventions(encryptionService);
        }
    }
}
