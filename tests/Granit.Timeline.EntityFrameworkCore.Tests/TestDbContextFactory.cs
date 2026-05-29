using Granit.DataFiltering;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

/// <summary>
/// Creates <see cref="TimelineHostDbContext"/> instances backed by SQLite in-memory for
/// fast, isolated integration tests that support all relational operations (including
/// <c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>).
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<TimelineHostDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TimelineHostDbContext> _options;
    private readonly IDataFilter? _dataFilter;

    private TestDbContextFactory(
        SqliteConnection connection,
        DbContextOptions<TimelineHostDbContext> options,
        IDataFilter? dataFilter)
    {
        _connection = connection;
        _options = options;
        _dataFilter = dataFilter;
    }

    public DbContextOptions<TimelineHostDbContext> Options => _options;

    public static TestDbContextFactory Create(IDataFilter? dataFilter = null)
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<TimelineHostDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<TimelineHostDbContext> options = optionsBuilder.Options;

        using (TimelineHostDbContext db = new(options, GranitDesignTime.CurrentTenant, dataFilter))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options, dataFilter);
    }

    public TimelineHostDbContext CreateDbContext()
        => new(_options, GranitDesignTime.CurrentTenant, _dataFilter);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Model customizer that remaps PostgreSQL-specific types (DateTimeOffset) to
/// SQLite-compatible types with value converters.
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
