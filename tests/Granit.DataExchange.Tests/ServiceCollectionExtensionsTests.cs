using Granit.DataExchange.Export;
using Granit.DataExchange.Extensions;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Tests.Mapping;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitDataImport_registers_semantic_mapping_service()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataImport();

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        ISemanticMappingService service = provider.GetRequiredService<ISemanticMappingService>();
        service.ShouldBeOfType<NullSemanticMappingService>();
    }

    [Fact]
    public void AddGranitDataImport_registers_mapping_suggestion_service()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataImport();

        // Assert
        services.ShouldContain(d =>
            d.ServiceType == typeof(IMappingSuggestionService) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataImport_registers_import_orchestrator()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataImport();

        // Assert
        services.ShouldContain(d =>
            d.ServiceType == typeof(IImportOrchestrator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataImport_returns_service_collection_for_chaining()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitDataImport();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddImportDefinition_registers_definition_as_singleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddImportDefinition<TestPatient, TestPatientImportDefinition>();

        // Assert
        services.ShouldContain(d =>
            d.ServiceType == typeof(ImportDefinition<TestPatient>) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSemanticMappingService_replaces_default_implementation()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddGranitDataImport();

        // Act
        services.AddSemanticMappingService<FakeSemanticMappingService>();

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        ISemanticMappingService service = provider.GetRequiredService<ISemanticMappingService>();
        service.ShouldBeOfType<FakeSemanticMappingService>();
    }

    [Fact]
    public void AddGranitDataImport_does_not_replace_existing_semantic_service()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<ISemanticMappingService, FakeSemanticMappingService>();

        // Act
        services.AddGranitDataImport();

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        ISemanticMappingService service = provider.GetRequiredService<ISemanticMappingService>();
        service.ShouldBeOfType<FakeSemanticMappingService>();
    }

    [Fact]
    public void AddGranitDataImport_registers_local_event_bus()
    {
        ServiceCollection services = new();

        services.AddGranitDataImport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ILocalEventBus));
    }

    // ---- Export registration ----------------------------------------

    [Fact]
    public void AddGranitDataExport_registers_export_orchestrator()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportOrchestrator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExport_registers_null_job_reader()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportJobReader) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExport_registers_null_job_writer()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportJobWriter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExport_registers_null_preset_reader()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportPresetReader) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExport_registers_null_preset_writer()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportPresetWriter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExport_registers_local_event_bus()
    {
        ServiceCollection services = new();

        services.AddGranitDataExport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ILocalEventBus));
    }

    [Fact]
    public void AddGranitDataExport_returns_service_collection_for_chaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitDataExport();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExportDefinition_registers_definition_as_singleton()
    {
        ServiceCollection services = new();

        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ExportDefinition<TestExportEntity>) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddExportDefinition_registers_descriptor_as_singleton()
    {
        ServiceCollection services = new();

        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportDefinitionDescriptor) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddExportDefinition_descriptor_resolves_to_definition_instance()
    {
        ServiceCollection services = new();
        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();
        ServiceProvider provider = services.BuildServiceProvider();

        IExportDefinitionDescriptor descriptor = provider.GetRequiredService<IExportDefinitionDescriptor>();

        descriptor.Name.ShouldBe("Test.ExportEntity");
        descriptor.EntityType.ShouldBe(typeof(TestExportEntity));
    }

    // ---- Test helpers ------------------------------------------------

    private sealed class TestExportEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestExportEntityDefinition : ExportDefinition<TestExportEntity>
    {
        public override string Name => "Test.ExportEntity";

        protected override void Configure(ExportDefinitionBuilder<TestExportEntity> builder) =>
            builder.Field(e => e.Name);
    }

    private sealed class FakeSemanticMappingService : ISemanticMappingService
    {
        public bool IsAvailable => true;

        public Task<IReadOnlyList<SemanticMappingSuggestion>> SuggestSemanticMappingsAsync(
            IReadOnlyList<string> headers,
            IReadOnlyList<ImportFieldMetadata> targetFields,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SemanticMappingSuggestion>>([]);
    }
}
