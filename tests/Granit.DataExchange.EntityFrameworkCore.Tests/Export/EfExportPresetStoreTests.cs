using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Export;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Export;

public sealed class EfExportPresetStoreTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    // ---- SaveAsync + GetAsync ----------------------------------------

    [Fact]
    public async Task SaveAsync_then_GetAsync_returns_preset()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);
        ExportPreset preset = new("Test.Export", "Monthly",
            ["Name", "Email"], "xlsx", false);

        // Act
        await sut.SaveAsync(preset, TestContext.Current.CancellationToken);
        ExportPreset? loaded = await sut.GetAsync(
            "Test.Export", "Monthly", TestContext.Current.CancellationToken);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DefinitionName.ShouldBe("Test.Export");
        loaded.PresetName.ShouldBe("Monthly");
        loaded.SelectedFields.ShouldBe(["Name", "Email"]);
        loaded.Format.ShouldBe("xlsx");
        loaded.IncludeIdForImport.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_upserts_existing_preset()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);
        ExportPreset original = new("Test.Export", "Monthly",
            ["Name"], "xlsx", false);
        ExportPreset updated = new("Test.Export", "Monthly",
            ["Name", "Email", "BirthDate"], "csv", true);

        // Act
        await sut.SaveAsync(original, TestContext.Current.CancellationToken);
        await sut.SaveAsync(updated, TestContext.Current.CancellationToken);
        ExportPreset? loaded = await sut.GetAsync(
            "Test.Export", "Monthly", TestContext.Current.CancellationToken);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.SelectedFields.ShouldBe(["Name", "Email", "BirthDate"]);
        loaded.Format.ShouldBe("csv");
        loaded.IncludeIdForImport.ShouldBeTrue();
    }

    // ---- ListAsync ---------------------------------------------------

    [Fact]
    public async Task ListAsync_returns_presets_for_definition()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);
        await sut.SaveAsync(new ExportPreset("Test.Export", "Alpha", ["Name"], "xlsx", false),
            TestContext.Current.CancellationToken);
        await sut.SaveAsync(new ExportPreset("Test.Export", "Beta", ["Email"], "csv", true),
            TestContext.Current.CancellationToken);
        await sut.SaveAsync(new ExportPreset("Other.Export", "Gamma", ["Id"], "xlsx", false),
            TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<ExportPreset> presets = await sut.ListAsync(
            "Test.Export", TestContext.Current.CancellationToken);

        // Assert
        presets.Count.ShouldBe(2);
        presets[0].PresetName.ShouldBe("Alpha");
        presets[1].PresetName.ShouldBe("Beta");
    }

    [Fact]
    public async Task ListAsync_empty_returns_empty_list()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);

        // Act
        IReadOnlyList<ExportPreset> presets = await sut.ListAsync(
            "NonExistent", TestContext.Current.CancellationToken);

        // Assert
        presets.ShouldBeEmpty();
    }

    // ---- DeleteAsync -------------------------------------------------

    [Fact]
    public async Task DeleteAsync_removes_preset()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);
        await sut.SaveAsync(new ExportPreset("Test.Export", "Monthly", ["Name"], "xlsx", false),
            TestContext.Current.CancellationToken);

        // Act
        await sut.DeleteAsync("Test.Export", "Monthly", TestContext.Current.CancellationToken);
        ExportPreset? loaded = await sut.GetAsync(
            "Test.Export", "Monthly", TestContext.Current.CancellationToken);

        // Assert
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_nonexistent_does_not_throw()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);

        // Act
        await sut.DeleteAsync("Test.Export", "NonExistent", TestContext.Current.CancellationToken);

        // Assert — preset still absent
        ExportPreset? result = await sut.GetAsync(
            "Test.Export", "NonExistent", TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    // ---- GetAsync with unknown preset --------------------------------

    [Fact]
    public async Task GetAsync_unknown_preset_returns_null()
    {
        // Arrange
        string dbName = NewDb();
        EfExportPresetStore sut = CreateStore(dbName);

        // Act
        ExportPreset? result = await sut.GetAsync(
            "Test.Export", "Unknown", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    // ---- Helpers -----------------------------------------------------

    private static EfExportPresetStore CreateStore(string dbName, Guid? tenantId = null)
    {
        InMemoryDataExchangeContextFactory factory = new(dbName);
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(tenantId.HasValue);
        if (tenantId.HasValue)
        {
            tenant.Id.Returns(tenantId.Value);
        }

        return new EfExportPresetStore(factory, clock, new SimpleGuidGenerator(), tenant);
    }
}
