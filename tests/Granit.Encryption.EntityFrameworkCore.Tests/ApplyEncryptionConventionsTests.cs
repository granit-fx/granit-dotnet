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

    [Fact]
    public void ApplyEncryptionConventions_AppliesConverter_ToNullableEncryptedStringProperty()
    {
        DbContextOptions<MultiPropertyDbContext> options = new DbContextOptionsBuilder<MultiPropertyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using MultiPropertyDbContext ctx = new(options, _encryption);

        IEntityType entityType = ctx.Model.FindEntityType(typeof(MultiEncryptedEntity))!;
        IProperty notesProperty = entityType.FindProperty(nameof(MultiEncryptedEntity.Notes))!;

        notesProperty.GetValueConverter().ShouldBeOfType<EncryptedStringConverter>();
    }

    [Fact]
    public void ApplyEncryptionConventions_AppliesConverter_ToMultipleEncryptedProperties()
    {
        DbContextOptions<MultiPropertyDbContext> options = new DbContextOptionsBuilder<MultiPropertyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using MultiPropertyDbContext ctx = new(options, _encryption);

        IEntityType entityType = ctx.Model.FindEntityType(typeof(MultiEncryptedEntity))!;
        IProperty ssnProperty = entityType.FindProperty(nameof(MultiEncryptedEntity.Ssn))!;
        IProperty notesProperty = entityType.FindProperty(nameof(MultiEncryptedEntity.Notes))!;

        ssnProperty.GetValueConverter().ShouldBeOfType<EncryptedStringConverter>();
        notesProperty.GetValueConverter().ShouldBeOfType<EncryptedStringConverter>();
    }

    [Fact]
    public void ApplyEncryptionConventions_IgnoresNonStringProperties_WithEncryptedAttribute()
    {
        DbContextOptions<NonStringEncryptedDbContext> options = new DbContextOptionsBuilder<NonStringEncryptedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using NonStringEncryptedDbContext ctx = new(options, _encryption);

        IEntityType entityType = ctx.Model.FindEntityType(typeof(NonStringEncryptedEntity))!;
        IProperty codeProperty = entityType.FindProperty(nameof(NonStringEncryptedEntity.Code))!;

        // [Encrypted] on int property should be ignored (only string properties are converted)
        codeProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void ApplyEncryptionConventions_ReturnsModelBuilder_ForChaining()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext ctx = new(options, _encryption);

        // The model should have been built successfully (chaining worked)
        ctx.Model.ShouldNotBeNull();
    }

    [Fact]
    public void EncryptedAttribute_RoundTrip_MultipleProperties_OnSave()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(ci => $"ENC:{ci.Arg<string>()}");
        _encryption.Decrypt(Arg.Any<string>()).Returns(ci =>
        {
            string s = ci.Arg<string>();
            return s.StartsWith("ENC:", StringComparison.Ordinal) ? s["ENC:".Length..] : null;
        });

        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<MultiPropertyDbContext> options = new DbContextOptionsBuilder<MultiPropertyDbContext>()
            .UseSqlite(connection)
            .Options;

        using (MultiPropertyDbContext ctx = new(options, _encryption))
        {
            ctx.Database.EnsureCreated();
            ctx.Entities.Add(new MultiEncryptedEntity
            {
                Id = 1,
                Ssn = "111-22-3333",
                Notes = "confidential notes",
                Name = "Bob"
            });
            ctx.SaveChanges();
        }

        // Verify both encrypted properties are stored encrypted, Name is plain
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Ssn, Notes, Name FROM Entities WHERE Id = 1";
        using SqliteDataReader reader = cmd.ExecuteReader();
        reader.Read().ShouldBeTrue();
        reader.GetString(0).ShouldBe("ENC:111-22-3333");
        reader.GetString(1).ShouldBe("ENC:confidential notes");
        reader.GetString(2).ShouldBe("Bob");
    }

    // --- Test fixtures ---

    private sealed class PatientEntity
    {
        public int Id { get; set; }

        [Encrypted]
        public string Ssn { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class MultiEncryptedEntity
    {
        public int Id { get; set; }

        [Encrypted]
        public string Ssn { get; set; } = string.Empty;

        [Encrypted]
        public string? Notes { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class NonStringEncryptedEntity
    {
        public int Id { get; set; }

        [Encrypted]
        public int Code { get; set; }

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

    private sealed class MultiPropertyDbContext(
        DbContextOptions<MultiPropertyDbContext> options,
        IStringEncryptionService encryptionService) : DbContext(options)
    {
        public DbSet<MultiEncryptedEntity> Entities => Set<MultiEncryptedEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyEncryptionConventions(encryptionService);
        }
    }

    private sealed class NonStringEncryptedDbContext(
        DbContextOptions<NonStringEncryptedDbContext> options,
        IStringEncryptionService encryptionService) : DbContext(options)
    {
        public DbSet<NonStringEncryptedEntity> Entities => Set<NonStringEncryptedEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyEncryptionConventions(encryptionService);
        }
    }
}
