using Granit.Encryption;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// Creates <see cref="IdentityDbContext"/> instances backed by SQLite
/// in-memory for fast, isolated relational tests. Mirrors the pattern
/// used by every other <c>*.EntityFrameworkCore.Tests</c> project — see
/// <c>Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests</c> for the
/// same shape against a different DbContext.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<IdentityDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;

    private TestDbContextFactory(SqliteConnection connection, DbContextOptions<IdentityDbContext> options)
    {
        _connection = connection;
        Options = options;
    }

    public DbContextOptions<IdentityDbContext> Options { get; }

    public static TestDbContextFactory Create()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<IdentityDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<IdentityDbContext> options = optionsBuilder.Options;

        // Create the schema once for the connection lifetime.
        using (IdentityDbContext db = new(options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options);
    }

    public IdentityDbContext CreateDbContext() => new(Options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant);

    public void Dispose() => _connection.Dispose();

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }
}

/// <summary>
/// Model customizer that remaps PostgreSQL-specific types
/// (<see cref="DateTimeOffset"/>) to SQLite-compatible types with value
/// converters. Same shape as the existing companion test factories.
/// </summary>
internal sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
        v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
