using System.Data.Common;
using Granit.Testing.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Suites;

/// <summary>
/// Proves the enum-as-string persistence convention at the raw column level: the database
/// stores the PascalCase value name, not the ordinal.
/// </summary>
public abstract class EnumPersistenceConformanceSuite(IRelationalConformanceFixture fixture)
{
    [Fact]
    public async Task Enum_is_stored_as_its_PascalCase_name()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();

        await using ConformanceDbContext db = await harness.CreateContextAsync();
        ConformanceOrder order = new() { Label = "enum", Status = ConformanceOrderStatus.Approved };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Read the raw column with ADO — double-quoted identifiers parse on both PostgreSQL
        // and SQL Server (QUOTED_IDENTIFIER ON is the ADO default).
        IEntityType entityType = db.Model.FindEntityType(typeof(ConformanceOrder))!;
        string table = entityType.GetTableName()!;
        string? schema = entityType.GetSchema();
        string statusColumn = entityType.FindProperty(nameof(ConformanceOrder.Status))!.GetColumnName();
        string idColumn = entityType.FindProperty(nameof(ConformanceOrder.Id))!.GetColumnName();

        DbConnection connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using DbCommand command = connection.CreateCommand();
        string qualifiedTable = schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
        command.CommandText = $"SELECT \"{statusColumn}\" FROM {qualifiedTable} WHERE \"{idColumn}\" = @id";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = order.Id;
        command.Parameters.Add(parameter);

        object? raw = await command.ExecuteScalarAsync();
        raw.ShouldBe("Approved",
            $"{fixture.ProviderName}: enum columns must store the PascalCase value name, not the ordinal");
    }

    [Fact]
    public async Task Enum_roundtrips_through_the_string_column()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();
        string label = $"enum-rt-{Guid.CreateVersion7():N}";

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Orders.Add(new ConformanceOrder { Label = label, Status = ConformanceOrderStatus.Rejected });
            await db.SaveChangesAsync();
        }

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            ConformanceOrder reloaded = await db.Orders.SingleAsync(o => o.Label == label);
            reloaded.Status.ShouldBe(ConformanceOrderStatus.Rejected);
        }
    }
}
