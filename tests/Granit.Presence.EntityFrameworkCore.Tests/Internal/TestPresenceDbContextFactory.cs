using Granit.Persistence.EntityFrameworkCore;
using Granit.Presence.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Presence.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Creates <see cref="PresenceDbContext"/> instances backed by SQLite in-memory for fast,
/// isolated relational tests of <see cref="EfPresenceStore"/>. Mirrors the canonical
/// pattern used by the other <c>*.EntityFrameworkCore.Tests</c> projects (see
/// <c>Granit.Identity.EntityFrameworkCore.Tests.TestDbContextFactory</c>). A single open
/// connection is shared so the in-memory database survives across the factory's
/// per-operation contexts.
/// </summary>
internal sealed class TestPresenceDbContextFactory : IDbContextFactory<PresenceDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PresenceDbContext> _options;

    private TestPresenceDbContextFactory(SqliteConnection connection, DbContextOptions<PresenceDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static TestPresenceDbContextFactory Create()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<PresenceDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<PresenceDbContext> options = optionsBuilder.Options;

        using (var db = new PresenceDbContext(options, GranitDesignTime.CurrentTenant))
        {
            db.Database.EnsureCreated();
        }

        return new TestPresenceDbContextFactory(connection, options);
    }

    public PresenceDbContext CreateDbContext() =>
        new(_options, GranitDesignTime.CurrentTenant);

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Remaps PostgreSQL-specific <see cref="DateTimeOffset"/> columns to SQLite-compatible
    /// storage with value converters. Same shape as the companion test factories.
    /// </summary>
    private sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
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
}
