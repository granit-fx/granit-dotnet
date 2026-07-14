using Granit.DataExchange.Export;
using Granit.DataExchange.Extensions;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Internal;
using Granit.DataExchange.Retention;
using Granit.DataExchange.Retention.Internal;
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
    public void AddGranitDataImport_registers_fail_fast_retention_store()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataImport();

        // Assert
        services.ShouldContain(d =>
            d.ServiceType == typeof(IDataExchangeRetentionStore) &&
            d.ImplementationType == typeof(NullDataExchangeRetentionStore) &&
            d.Lifetime == ServiceLifetime.Scoped);
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

    [Fact]
    public void AddExportDefinition_registers_entity_binding_as_singleton()
    {
        ServiceCollection services = new();

        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IExportEntityBinding) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddExportDefinition_binding_recovers_typed_definition_via_visitor()
    {
        ServiceCollection services = new();
        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();
        ServiceProvider provider = services.BuildServiceProvider();

        IExportEntityBinding binding = provider.GetRequiredService<IExportEntityBinding>();

        binding.Descriptor.ShouldBeSameAs(provider.GetRequiredService<ExportDefinition<TestExportEntity>>());
        Type visitedEntityType = binding.Accept(new EntityTypeProbeVisitor());
        visitedEntityType.ShouldBe(typeof(TestExportEntity));
    }

    [Fact]
    public void AddExportDefinition_is_idempotent_for_same_pair()
    {
        ServiceCollection services = new();

        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();
        services.AddExportDefinition<TestExportEntity, TestExportEntityDefinition>();

        services.Count(d => d.ServiceType == typeof(ExportDefinition<TestExportEntity>)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IExportEntityBinding)).ShouldBe(1);
    }

    private sealed class EntityTypeProbeVisitor : IExportEntityVisitor<Type>
    {
        public Type Visit<TEntity>(ExportDefinition<TEntity> definition)
            where TEntity : class =>
            typeof(TEntity);
    }

    [Fact]
    public void AddGranitDataImport_registers_fail_fast_file_provider_singleton()
    {
        ServiceCollection services = new();

        services.AddGranitDataImport();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDataExchangeFileProvider) &&
            d.ImplementationType == typeof(NullDataExchangeFileProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddInMemoryDataExchangeFileProvider_replaces_default_with_singleton()
    {
        ServiceCollection services = new();
        services.AddGranitDataImport();

        services.AddInMemoryDataExchangeFileProvider();

        services.Count(d => d.ServiceType == typeof(IDataExchangeFileProvider)).ShouldBe(1);
        services.ShouldContain(d =>
            d.ServiceType == typeof(IDataExchangeFileProvider) &&
            d.ImplementationType == typeof(InMemoryDataExchangeFileProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task InMemory_file_provider_roundtrips_across_scopes()
    {
        // Upload happens in one DI scope (HTTP request), execution in another
        // (second request or message handler) — the file must survive the scope change.
        ServiceCollection services = new();
        services.AddGranitDataImport();
        services.AddInMemoryDataExchangeFileProvider();
        await using ServiceProvider provider = services.BuildServiceProvider();

        Granit.Domain.ValueObjects.BlobReference reference;
        using (IServiceScope uploadScope = provider.CreateScope())
        {
            await using MemoryStream content = new([1, 2, 3]);
            reference = await uploadScope.ServiceProvider
                .GetRequiredService<IDataExchangeFileProvider>()
                .SaveAsync("data.csv", content, TestContext.Current.CancellationToken);
        }

        using IServiceScope executionScope = provider.CreateScope();
        await using Stream reopened = await executionScope.ServiceProvider
            .GetRequiredService<IDataExchangeFileProvider>()
            .OpenAsync(reference, TestContext.Current.CancellationToken);

        MemoryStream buffer = new();
        await reopened.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        buffer.ToArray().ShouldBe([1, 2, 3]);
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
