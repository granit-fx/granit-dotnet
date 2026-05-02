using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Reader/writer integration tests using SQLite in-memory. Exercises real
/// relational pipeline (the InMemory provider skips value converters).
/// </summary>
public sealed class EfEntityCustomizationReaderWriterTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<CustomizationDbContext> _options = null!;
    private IDbContextFactory<CustomizationDbContext> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<CustomizationDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using (var ctx = new CustomizationDbContext(_options))
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        _factory = Substitute.For<IDbContextFactory<CustomizationDbContext>>();
        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new CustomizationDbContext(_options)));
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Upsert_then_Get_returns_persisted_customization()
    {
        EfEntityCustomizationWriter writer = new(_factory);
        EfEntityCustomizationReader reader = new(_factory);

        var original = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: "Granit.Parties.Party",
            layoutKind: LayoutKind.FormDefault,
            deltas: [new HideDelta("legacyCode")]);

        await writer.UpsertAsync(original, TestContext.Current.CancellationToken);

        EntityCustomization? loaded = await reader.GetAsync(
            "Granit.Parties.Party", LayoutKind.FormDefault, tenantId: null,
            TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Deltas.Count.ShouldBe(1);
        loaded.Deltas[0].FieldName.ShouldBe("legacyCode");
    }

    [Fact]
    public async Task Upsert_with_existing_lookup_replaces_deltas_in_place()
    {
        EfEntityCustomizationWriter writer = new(_factory);
        EfEntityCustomizationReader reader = new(_factory);

        var originalId = Guid.NewGuid();
        await writer.UpsertAsync(
            EntityCustomization.Create(
                id: originalId,
                entityName: "Granit.Parties.Party",
                layoutKind: LayoutKind.List,
                deltas: [new HideDelta("a"), new HideDelta("b")]),
            TestContext.Current.CancellationToken);

        // Second upsert with a different id — should replace deltas on the
        // existing row, NOT create a duplicate (the unique index would fail
        // anyway, but the writer is supposed to handle this gracefully).
        await writer.UpsertAsync(
            EntityCustomization.Create(
                id: Guid.NewGuid(),
                entityName: "Granit.Parties.Party",
                layoutKind: LayoutKind.List,
                deltas: [new HideDelta("c")]),
            TestContext.Current.CancellationToken);

        EntityCustomization? loaded = await reader.GetAsync(
            "Granit.Parties.Party", LayoutKind.List, tenantId: null,
            TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(originalId, "writer should preserve the existing row id");
        loaded.Deltas.Count.ShouldBe(1);
        loaded.Deltas[0].FieldName.ShouldBe("c");
    }

    [Fact]
    public async Task Get_with_unknown_layout_returns_null()
    {
        EfEntityCustomizationReader reader = new(_factory);

        EntityCustomization? loaded = await reader.GetAsync(
            "Granit.Parties.Party", LayoutKind.Calendar, tenantId: null,
            TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_removes_the_row()
    {
        EfEntityCustomizationWriter writer = new(_factory);
        EfEntityCustomizationReader reader = new(_factory);

        var id = Guid.NewGuid();
        await writer.UpsertAsync(
            EntityCustomization.Create(
                id: id,
                entityName: "Granit.Parties.Party",
                layoutKind: LayoutKind.Gallery,
                deltas: [new HideDelta("legacy")]),
            TestContext.Current.CancellationToken);

        await writer.DeleteAsync(id, TestContext.Current.CancellationToken);

        EntityCustomization? loaded = await reader.GetAsync(
            "Granit.Parties.Party", LayoutKind.Gallery, tenantId: null,
            TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task GetForTenant_returns_every_row_ordered()
    {
        EfEntityCustomizationWriter writer = new(_factory);
        EfEntityCustomizationReader reader = new(_factory);

        await writer.UpsertAsync(EntityCustomization.Create(
            Guid.NewGuid(), "Granit.Parties.Party", LayoutKind.List, [new HideDelta("a")]),
            TestContext.Current.CancellationToken);
        await writer.UpsertAsync(EntityCustomization.Create(
            Guid.NewGuid(), "Granit.Parties.Party", LayoutKind.FormDefault, [new HideDelta("b")]),
            TestContext.Current.CancellationToken);
        await writer.UpsertAsync(EntityCustomization.Create(
            Guid.NewGuid(), "Granit.Activities.Activity", LayoutKind.List, [new HideDelta("c")]),
            TestContext.Current.CancellationToken);

        IReadOnlyList<EntityCustomization> rows = await reader.GetForTenantAsync(
            tenantId: null, TestContext.Current.CancellationToken);

        rows.Count.ShouldBe(3);
        rows.Select(r => r.EntityName).ShouldBe(
            ["Granit.Activities.Activity", "Granit.Parties.Party", "Granit.Parties.Party"]);
    }
}
