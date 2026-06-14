using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.AI.Prompts.EntityFrameworkCore.Tests;

/// <summary>
/// Creates <see cref="AIPromptsDbContext"/> instances backed by SQLite in-memory for fast, isolated
/// relational tests.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<AIPromptsDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AIPromptsDbContext> _options;

    private TestDbContextFactory(SqliteConnection connection, DbContextOptions<AIPromptsDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static TestDbContextFactory Create()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<AIPromptsDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<AIPromptsDbContext> options = optionsBuilder.Options;

        using (AIPromptsDbContext db = new(options, GranitDesignTime.CurrentTenant))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options);
    }

    public AIPromptsDbContext CreateDbContext() => new(_options, GranitDesignTime.CurrentTenant);

    public void Dispose() => _connection.Dispose();
}

/// <summary>Remaps <see cref="DateTimeOffset"/> to a SQLite-compatible type.</summary>
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
