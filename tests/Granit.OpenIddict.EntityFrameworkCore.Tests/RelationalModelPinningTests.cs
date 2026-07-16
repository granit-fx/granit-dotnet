using System.Text;
using Granit.Identity.Local.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

/// <summary>
/// Golden-master snapshot of the full relational model — tables, columns (name, CLR type,
/// nullability, maxlength), primary keys, indexes (columns, uniqueness, name, filter) and named query
/// filters. Any change to the produced schema flips this test.
/// </summary>
/// <remarks>
/// The model is now split across <c>IdentityLocalDbContext</c> and <c>OpenIddictDbContext</c>; a host
/// migration context owns the whole schema by applying both <c>ConfigureGranitIdentityLocal</c> and
/// <c>ConfigureGranitOpenIddict</c>. This test builds exactly such a composed context and asserts it
/// stays byte-identical to the pre-split consolidated snapshot — proving the decomposition emits no
/// data migration. It also guards every change against accidental schema drift (a dropped unique
/// index, a widened nullability, a lost filter). Provider-agnostic: store types are excluded so the
/// snapshot holds across PostgreSQL/SQLite.
/// </remarks>
public sealed class RelationalModelPinningTests
{
    [Fact]
    public void ComposedHostModel_MatchesPinnedSnapshot()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<CompositeMigrationDbContext> options =
            new DbContextOptionsBuilder<CompositeMigrationDbContext>()
                .UseSqlite(connection)
                .Options;
        using CompositeMigrationDbContext context = new(options, new FakeCurrentTenant());

        string snapshot = BuildSnapshot(context.Model);

        // A diff here is intentional: regenerate RelationalModel.approved.txt only when the schema
        // change is deliberate. The composed host model must stay byte-identical (zero data migration).
        snapshot.ShouldBe(ReadApprovedSnapshot());
    }

    /// <summary>
    /// Stand-in for a host migration context that owns the whole schema, mirroring the
    /// <c>ShowcaseHostDbContext</c> pattern: a single <see cref="GranitDbContext"/> that applies both
    /// module model builders.
    /// </summary>
    private sealed class CompositeMigrationDbContext(
        DbContextOptions<CompositeMigrationDbContext> options, ICurrentTenant currentTenant)
        : GranitDbContext(options, currentTenant)
    {
        protected override bool TenantFilterTreatsNullAsGlobal => true;

        protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureGranitIdentityLocal();
            modelBuilder.ConfigureGranitOpenIddict();
        }
    }

    private static string BuildSnapshot(IModel model)
    {
        StringBuilder sb = new();
        void Line(string text) => sb.Append(text).Append('\n');

        IEnumerable<IEntityType> entityTypes = model.GetEntityTypes()
            .OrderBy(e => e.GetTableName() ?? e.Name, StringComparer.Ordinal)
            .ThenBy(e => e.Name, StringComparer.Ordinal);

        foreach (IEntityType entityType in entityTypes)
        {
            string table = entityType.GetTableName() ?? "(none)";
            Line($"TABLE {table} [{entityType.Name}]");

            foreach (IProperty property in entityType.GetProperties()
                .OrderBy(p => p.GetColumnName(), StringComparer.Ordinal))
            {
                string nullability = property.IsNullable ? "NULL" : "NOT NULL";
                int? maxLength = property.GetMaxLength();
                string len = maxLength.HasValue ? $" len={maxLength.Value}" : string.Empty;
                Line($"  COL {property.GetColumnName()} {TypeName(property.ClrType)} {nullability}{len}");
            }

            IKey? pk = entityType.FindPrimaryKey();
            if (pk is not null)
            {
                Line($"  PK {ColumnList(pk.Properties)}");
            }

            IEnumerable<(string Line, string Sort)> indexes = entityType.GetIndexes()
                .Select(i =>
                {
                    string cols = ColumnList(i.Properties);
                    string unique = i.IsUnique ? " UNIQUE" : string.Empty;
                    string name = i.GetDatabaseName() ?? "(none)";
                    string filter = i.GetFilter() is { Length: > 0 } f ? $" FILTER[{f}]" : string.Empty;
                    return (Line: $"  INDEX {cols}{unique} name={name}{filter}", Sort: cols + "|" + name);
                })
                .OrderBy(i => i.Sort, StringComparer.Ordinal);
            foreach ((string line, string _) in indexes)
            {
                Line(line);
            }

            foreach (string filter in entityType.GetDeclaredQueryFilters()
                .Select(f => f.Key ?? "(anonymous)")
                .OrderBy(k => k, StringComparer.Ordinal))
            {
                Line($"  FILTER {filter}");
            }
        }

        return sb.ToString();
    }

    private static string ColumnList(IReadOnlyList<IProperty> properties) =>
        string.Join(", ", properties.Select(p => p.GetColumnName()));

    private static string TypeName(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        return underlying is not null ? underlying.Name + "?" : type.Name;
    }

    private static string ReadApprovedSnapshot()
    {
        Type anchor = typeof(RelationalModelPinningTests);
        string resourceName = $"{anchor.Namespace}.RelationalModel.approved.txt";
        using Stream stream = anchor.Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded snapshot '{resourceName}' not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
