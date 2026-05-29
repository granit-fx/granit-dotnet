using Granit.DataFiltering;
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
/// Creates <see cref="IdentityHostDbContext"/> instances backed by SQLite in-memory for
/// fast, isolated relational tests. Mirrors the pattern used by every other
/// <c>*.EntityFrameworkCore.Tests</c> project.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<IdentityHostDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IdentityHostDbContext> _options;
    private readonly IDataFilter? _dataFilter;

    private TestDbContextFactory(
        SqliteConnection connection,
        DbContextOptions<IdentityHostDbContext> options,
        IDataFilter? dataFilter)
    {
        _connection = connection;
        _options = options;
        _dataFilter = dataFilter;
    }

    public DbContextOptions<IdentityHostDbContext> Options => _options;

    public static TestDbContextFactory Create(IDataFilter? dataFilter = null)
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<IdentityHostDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<IdentityHostDbContext> options = optionsBuilder.Options;

        using (IdentityHostDbContext db = new(options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant, dataFilter))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options, dataFilter);
    }

    public IdentityHostDbContext CreateDbContext()
        => new(_options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant, _dataFilter);

    public void Dispose() => _connection.Dispose();

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }
}

/// <summary>
/// Model customizer that remaps PostgreSQL-specific types (<see cref="DateTimeOffset"/>)
/// to SQLite-compatible types with value converters. Same shape as the existing companion
/// test factories.
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
