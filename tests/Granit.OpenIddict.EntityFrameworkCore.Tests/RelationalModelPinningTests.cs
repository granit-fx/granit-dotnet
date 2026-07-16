using System.Text;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

/// <summary>
/// Golden-master snapshot of the consolidated <see cref="OpenIddictDbContext"/> relational model —
/// tables, columns (name, CLR type, nullability), primary keys, indexes (columns, uniqueness) and
/// named query filters. Any change to the produced schema flips this test.
/// </summary>
/// <remarks>
/// This is the safety net for the Identity/OpenIddict DbContext decomposition (Phase 3): once the
/// model is split across <c>IdentityLocalDbContext</c> and <c>OpenIddictDbContext</c>, the composed
/// model a host builds must remain byte-identical to this snapshot so no data migration is emitted.
/// It also guards every other change against accidental schema drift (a dropped unique index, a
/// widened nullability, a lost filter). Provider-agnostic: store types are intentionally excluded so
/// the snapshot holds across PostgreSQL/SQLite.
/// </remarks>
public sealed class RelationalModelPinningTests
{
    [Fact]
    public void ConsolidatedModel_MatchesPinnedSnapshot()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseSqlite(connection)
                .Options;
        using OpenIddictDbContext context = new(options, new FakeCurrentTenant());

        string snapshot = BuildSnapshot(context.Model);

        // A diff here is intentional: regenerate RelationalModel.approved.txt only when the schema
        // change is deliberate. During the DbContext decomposition the composed model must keep this
        // snapshot byte-identical (zero data migration).
        snapshot.ShouldBe(ReadApprovedSnapshot());
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
                Line($"  COL {property.GetColumnName()} {TypeName(property.ClrType)} {nullability}");
            }

            IKey? pk = entityType.FindPrimaryKey();
            if (pk is not null)
            {
                Line($"  PK {ColumnList(pk.Properties)}");
            }

            IEnumerable<(string Cols, bool IsUnique)> indexes = entityType.GetIndexes()
                .Select(i => (Cols: ColumnList(i.Properties), i.IsUnique))
                .OrderBy(i => i.Cols, StringComparer.Ordinal);
            foreach ((string cols, bool isUnique) in indexes)
            {
                Line(isUnique ? $"  INDEX {cols} UNIQUE" : $"  INDEX {cols}");
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
