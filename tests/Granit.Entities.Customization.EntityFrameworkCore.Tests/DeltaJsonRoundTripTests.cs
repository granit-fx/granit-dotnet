using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies the <c>Deltas</c> JSON value-converter round-trips polymorphic
/// records through the column without losing the discriminator. Uses SQLite
/// in-memory so we exercise a real relational pipeline (the InMemory provider
/// skips value converters).
/// </summary>
public sealed class DeltaJsonRoundTripTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<CustomizationDbContext> _options = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<CustomizationDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var ctx = new CustomizationDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Mixed_delta_types_roundtrip_through_jsonb_column()
    {
        var id = Guid.NewGuid();
        var original = EntityCustomization.Create(
            id: id,
            entityName: "Granit.Parties.Party",
            layoutKind: LayoutKind.FormDefault,
            deltas:
            [
                new ReorderDelta("currency", BeforeFieldName: "total", AfterFieldName: null),
                new RegroupDelta("internalNotes", "internal"),
                new HideDelta("legacyCode"),
            ]);

        await using (var ctx = new CustomizationDbContext(_options))
        {
            ctx.EntityCustomizations.Add(original);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var ctx = new CustomizationDbContext(_options))
        {
            EntityCustomization? loaded = await ctx.EntityCustomizations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);

            loaded.ShouldNotBeNull();
            loaded.Deltas.Count.ShouldBe(3);
            loaded.Deltas[0].ShouldBeOfType<ReorderDelta>().ShouldSatisfyAllConditions(
                d => d.FieldName.ShouldBe("currency"),
                d => d.BeforeFieldName.ShouldBe("total"),
                d => d.AfterFieldName.ShouldBeNull());
            loaded.Deltas[1].ShouldBeOfType<RegroupDelta>().ShouldSatisfyAllConditions(
                d => d.FieldName.ShouldBe("internalNotes"),
                d => d.GroupKey.ShouldBe("internal"));
            loaded.Deltas[2].ShouldBeOfType<HideDelta>().FieldName.ShouldBe("legacyCode");
        }
    }

    [Fact]
    public async Task Empty_delta_list_roundtrips_as_empty_array()
    {
        var id = Guid.NewGuid();
        var original = EntityCustomization.Create(
            id: id,
            entityName: "Granit.Parties.Party",
            layoutKind: LayoutKind.List,
            deltas: []);

        await using (var ctx = new CustomizationDbContext(_options))
        {
            ctx.EntityCustomizations.Add(original);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var ctx = new CustomizationDbContext(_options))
        {
            EntityCustomization? loaded = await ctx.EntityCustomizations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);
            loaded.ShouldNotBeNull();
            loaded.Deltas.ShouldBeEmpty();
        }
    }
}
