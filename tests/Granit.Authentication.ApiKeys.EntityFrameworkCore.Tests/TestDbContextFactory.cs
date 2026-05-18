using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

/// <summary>
/// Creates <see cref="AuthenticationApiKeysDbContext"/> instances backed by SQLite in-memory
/// for fast, isolated integration tests that support all relational operations
/// (including <c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>).
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<AuthenticationApiKeysDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuthenticationApiKeysDbContext> _options;

    private TestDbContextFactory(SqliteConnection connection, DbContextOptions<AuthenticationApiKeysDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public DbContextOptions<AuthenticationApiKeysDbContext> Options => _options;

    public static TestDbContextFactory Create()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<AuthenticationApiKeysDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<AuthenticationApiKeysDbContext> options = optionsBuilder.Options;

        // Create the schema
        using (AuthenticationApiKeysDbContext db = new(options, GranitDesignTime.CurrentTenant))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options);
    }

    public AuthenticationApiKeysDbContext CreateDbContext() => new(_options, GranitDesignTime.CurrentTenant);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Model customizer that remaps PostgreSQL-specific types (DateTimeOffset)
/// to SQLite-compatible types with value converters.
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
