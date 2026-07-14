using Granit.DataExchange.Import;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// Import definition for <see cref="TestEntity"/> with Niss as business key.
/// </summary>
internal sealed class TestImportDefinition : ImportDefinition<TestEntity>
{
    public override string Name => "Test.TestEntityImport";

    protected override void Configure(ImportDefinitionBuilder<TestEntity> builder)
    {
        builder
            .HasBusinessKey(e => e.Niss)
            .Property(e => e.Name, p => p.DisplayName("Name").Required())
            .Property(e => e.Email, p => p.DisplayName("Email"))
            .Property(e => e.Niss, p => p.DisplayName("NISS"))
            .Property(e => e.Age, p => p.DisplayName("Age"));
    }
}

/// <summary>
/// Import definition for <see cref="TestEntity"/> with composite key (Name + Email).
/// </summary>
internal sealed class TestCompositeKeyImportDefinition : ImportDefinition<TestEntity>
{
    public override string Name => "Test.CompositeKeyImport";

    protected override void Configure(ImportDefinitionBuilder<TestEntity> builder)
    {
        builder
            .HasCompositeKey(e => e.Name, e => e.Email)
            .Property(e => e.Name, p => p.DisplayName("Name").Required())
            .Property(e => e.Email, p => p.DisplayName("Email").Required());
    }
}

/// <summary>
/// Import definition for <see cref="TestEntity"/> with external ID.
/// </summary>
internal sealed class TestExternalIdImportDefinition : ImportDefinition<TestEntity>
{
    public override string Name => "Test.ExternalIdImport";

    protected override void Configure(ImportDefinitionBuilder<TestEntity> builder)
    {
        builder
            .HasExternalId()
            .Property(e => e.Name, p => p.DisplayName("Name").Required())
            .Property(e => e.ExternalId, p => p.DisplayName("External ID"));
    }
}
