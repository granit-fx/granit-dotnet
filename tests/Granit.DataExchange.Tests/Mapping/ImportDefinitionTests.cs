using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class ImportDefinitionTests
{
    [Fact]
    public void GetProperties_returns_declared_properties()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Act
        IReadOnlyList<PropertyMapping> properties = definition.GetProperties();

        // Assert
        properties.Count.ShouldBe(4);
        properties.ShouldContain(p => p.PropertyPath == "Niss");
        properties.ShouldContain(p => p.PropertyPath == "FirstName");
        properties.ShouldContain(p => p.PropertyPath == "LastName");
        properties.ShouldContain(p => p.PropertyPath == "Email");
    }

    [Fact]
    public void GetFieldMetadata_returns_metadata_without_business_data()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Act
        IReadOnlyList<ImportFieldMetadata> metadata = definition.GetFieldMetadata();

        // Assert
        metadata.Count.ShouldBe(4);

        ImportFieldMetadata nissField = metadata.First(f => f.PropertyPath == "Niss");
        nissField.DisplayName.ShouldBe("NISS");
        nissField.IsRequired.ShouldBeTrue();
        nissField.ClrTypeName.ShouldBe("String");
    }

    [Fact]
    public void GetBusinessKeyProperties_returns_declared_keys()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Act
        IReadOnlyList<string> keys = definition.GetBusinessKeyProperties();

        // Assert
        keys.ShouldBe(["Niss"]);
    }

    [Fact]
    public void GetExcludedOnUpdateProperties_returns_excluded()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Act
        IReadOnlyList<string> excluded = definition.GetExcludedOnUpdateProperties();

        // Assert
        excluded.ShouldBe(["CreatedAt"]);
    }

    [Fact]
    public void Name_returns_configured_name()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Assert
        definition.Name.ShouldBe("Test.PatientImport");
    }

    [Fact]
    public void EntityType_returns_correct_type()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Assert
        definition.EntityType.ShouldBe(typeof(TestPatient));
    }

    [Fact]
    public void AllowedMimeTypes_returns_defaults()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Assert
        definition.AllowedMimeTypes.ShouldContain("text/csv");
        definition.AllowedMimeTypes.ShouldContain("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public void GetGroupByColumn_returns_null_when_not_configured()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Assert
        definition.GetGroupByColumn().ShouldBeNull();
    }

    [Fact]
    public void GetBuilder_is_idempotent()
    {
        // Arrange
        TestPatientImportDefinition definition = new();

        // Act
        IReadOnlyList<PropertyMapping> first = definition.GetProperties();
        IReadOnlyList<PropertyMapping> second = definition.GetProperties();

        // Assert
        first.Count.ShouldBe(second.Count);
    }
}

public sealed class TestPatient
{
    public string Niss { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TestPatientImportDefinition : ImportDefinition<TestPatient>
{
    public override string Name => "Test.PatientImport";

    protected override void Configure(ImportDefinitionBuilder<TestPatient> builder)
    {
        builder
            .HasBusinessKey(p => p.Niss)
            .Property(p => p.Niss, p => p.DisplayName("NISS").Aliases("Numéro national", "National ID").Required())
            .Property(p => p.FirstName, p => p.DisplayName("Prénom").Aliases("First Name"))
            .Property(p => p.LastName, p => p.DisplayName("Nom").Aliases("Last Name"))
            .Property(p => p.Email, p => p.DisplayName("Email").Aliases("Courriel", "Mail"))
            .ExcludeOnUpdate(p => p.CreatedAt);
    }
}
