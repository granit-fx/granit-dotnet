using Granit.DataExchange.Extensions;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Pipeline;

public sealed class ImportPipelineRegistryTests
{
    [Fact]
    public void Find_uses_ordinal_comparison()
    {
        ImportPipelineRegistry registry = new(
            [new ImportEntityBinding<Alpha>(new AlphaDefinition())]);

        registry.Find("Test.Alpha").ShouldNotBeNull();
        registry.Find("test.alpha").ShouldBeNull();
        registry.Find("TEST.ALPHA").ShouldBeNull();
    }

    [Fact]
    public void Find_returns_descriptor_with_entity_type_and_name()
    {
        ImportPipelineRegistry registry = new(
            [new ImportEntityBinding<Alpha>(new AlphaDefinition())]);

        IImportPipelineDescriptor descriptor = registry.Find("Test.Alpha")!;

        descriptor.DefinitionName.ShouldBe("Test.Alpha");
        descriptor.EntityType.ShouldBe(typeof(Alpha));
        descriptor.Definition.Name.ShouldBe("Test.Alpha");
    }

    [Fact]
    public void Constructor_duplicate_definition_names_throw()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            new ImportPipelineRegistry(
            [
                new ImportEntityBinding<Alpha>(new AlphaDefinition()),
                new ImportEntityBinding<Beta>(new DuplicateNameDefinition()),
            ]));

        ex.Message.ShouldContain("Duplicate import definition name 'Test.Alpha'");
        ex.Message.ShouldContain("Alpha");
        ex.Message.ShouldContain("Beta");
    }

    [Fact]
    public void GetAll_is_stable_ordered_by_name()
    {
        // Registered out of order — snapshot must come back ordinal-sorted.
        ImportPipelineRegistry registry = new(
        [
            new ImportEntityBinding<Beta>(new BetaDefinition()),
            new ImportEntityBinding<Alpha>(new AlphaDefinition()),
        ]);

        registry.GetAll().Select(d => d.DefinitionName)
            .ShouldBe(["Test.Alpha", "Test.Beta"]);
    }

    [Fact]
    public void AddImportDefinition_registers_binding_that_resolves_a_working_descriptor()
    {
        ServiceCollection services = new();
        services.AddImportDefinition<Alpha, AlphaDefinition>();
        services.AddSingleton<IImportPipelineRegistry, ImportPipelineRegistry>();
        using ServiceProvider provider = services.BuildServiceProvider();

        IImportPipelineRegistry registry = provider.GetRequiredService<IImportPipelineRegistry>();

        IImportPipelineDescriptor? descriptor = registry.Find("Test.Alpha");
        descriptor.ShouldNotBeNull();
        descriptor!.EntityType.ShouldBe(typeof(Alpha));
        descriptor.DefinitionName.ShouldBe("Test.Alpha");
    }

    [Fact]
    public void AddImportDefinition_is_idempotent()
    {
        ServiceCollection services = new();
        services.AddImportDefinition<Alpha, AlphaDefinition>();
        services.AddImportDefinition<Alpha, AlphaDefinition>();
        services.AddSingleton<IImportPipelineRegistry, ImportPipelineRegistry>();
        using ServiceProvider provider = services.BuildServiceProvider();

        // A duplicate binding would make the registry constructor throw.
        IImportPipelineRegistry registry = provider.GetRequiredService<IImportPipelineRegistry>();

        registry.GetAll().ShouldHaveSingleItem();
        provider.GetServices<IImportEntityBinding>().ShouldHaveSingleItem();
        provider.GetServices<IImportDefinitionDescriptor>().ShouldHaveSingleItem();
    }

    // ── Test fixtures ────────────────────────────────────────────────────────

    internal sealed class Alpha
    {
        public string? Name { get; set; }
    }

    internal sealed class Beta
    {
        public string? Name { get; set; }
    }

    internal sealed class AlphaDefinition : ImportDefinition<Alpha>
    {
        public override string Name => "Test.Alpha";

        protected override void Configure(ImportDefinitionBuilder<Alpha> builder) =>
            builder.Property(e => e.Name);
    }

    internal sealed class BetaDefinition : ImportDefinition<Beta>
    {
        public override string Name => "Test.Beta";

        protected override void Configure(ImportDefinitionBuilder<Beta> builder) =>
            builder.Property(e => e.Name);
    }

    internal sealed class DuplicateNameDefinition : ImportDefinition<Beta>
    {
        public override string Name => "Test.Alpha";

        protected override void Configure(ImportDefinitionBuilder<Beta> builder) =>
            builder.Property(e => e.Name);
    }
}
