using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Encryption;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

/// <summary>
/// Creates <see cref="NotificationsHostDbContext"/> instances backed by SQLite in-memory
/// for fast, isolated integration tests that support all relational operations (including
/// <c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>).
/// </summary>
/// <remarks>
/// The <see cref="IMultiTenant"/> query filter is disabled via a dedicated
/// <see cref="DataFilter"/> instance so tests can insert/read rows under arbitrary tenant
/// ids without setting up an ambient <c>ICurrentTenant</c>.
/// </remarks>
internal sealed class TestDbContextFactory : IDbContextFactory<NotificationsHostDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NotificationsHostDbContext> _options;
    private readonly IDataFilter _dataFilter;
    private readonly IDisposable _multiTenantDisabled;

    private TestDbContextFactory(
        SqliteConnection connection,
        DbContextOptions<NotificationsHostDbContext> options,
        IDataFilter dataFilter,
        IDisposable multiTenantDisabled)
    {
        _connection = connection;
        _options = options;
        _dataFilter = dataFilter;
        _multiTenantDisabled = multiTenantDisabled;
    }

    public static TestDbContextFactory Create()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptionsBuilder<NotificationsHostDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();

        DbContextOptions<NotificationsHostDbContext> options = optionsBuilder.Options;

        DataFilter dataFilter = new();
        IDisposable multiTenantDisabled = dataFilter.Disable<IMultiTenant>();

        using (NotificationsHostDbContext db = new(options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant, dataFilter))
        {
            db.Database.EnsureCreated();
        }

        return new TestDbContextFactory(connection, options, dataFilter, multiTenantDisabled);
    }

    public NotificationsHostDbContext CreateDbContext()
        => new(_options, new PassthroughEncryption(), GranitDesignTime.CurrentTenant, _dataFilter);

    public void Dispose()
    {
        _multiTenantDisabled.Dispose();
        _connection.Dispose();
    }

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }
}

/// <summary>
/// Model customizer that remaps PostgreSQL-specific types (jsonb, DateTimeOffset) to
/// SQLite-compatible types with value converters.
/// </summary>
internal sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    private static readonly ValueConverter<JsonElement, string> JsonElementConverter = new(
        v => v.GetRawText(),
        v => JsonDocument.Parse(v, default).RootElement);

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
                if (property.ClrType == typeof(JsonElement))
                {
                    property.SetColumnType("TEXT");
                    property.SetValueConverter(JsonElementConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset))
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
