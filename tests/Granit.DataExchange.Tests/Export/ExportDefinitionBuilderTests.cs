using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportDefinitionBuilderTests
{
    // ---- Simple Field ------------------------------------------------

    [Fact]
    public void Field_simple_property_adds_descriptor()
    {
        // Arrange
        ExportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.Field(e => e.Name);

        // Assert
        builder.Fields.Count.ShouldBe(1);
        ExportFieldDescriptor field = builder.Fields[0];
        field.PropertyPath.ShouldBe("Name");
        field.ClrTypeName.ShouldBe("String");
        field.Header.ShouldBeNull();
        field.Format.ShouldBeNull();
        field.IsNavigation.ShouldBeFalse();
    }

    [Fact]
    public void Field_with_header_sets_header()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Name, f => f.Header("Nom"));

        builder.Fields[0].Header.ShouldBe("Nom");
    }

    [Fact]
    public void Field_with_format_sets_format()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.BirthDate, f => f.Format("dd/MM/yyyy"));

        builder.Fields[0].Format.ShouldBe("dd/MM/yyyy");
    }

    [Fact]
    public void Field_with_order_sets_explicit_order()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Name, f => f.Order(10));

        builder.Fields[0].Order.ShouldBe(10);
    }

    [Fact]
    public void Fields_auto_increment_order()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Name);
        builder.Field(e => e.Email);

        builder.Fields[0].Order.ShouldBe(0);
        builder.Fields[1].Order.ShouldBe(1);
    }

    // ---- Navigation Field --------------------------------------------

    [Fact]
    public void Field_navigation_uses_dot_notation()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Company, c => c.Name);

        builder.Fields[0].PropertyPath.ShouldBe("Company.Name");
        builder.Fields[0].IsNavigation.ShouldBeTrue();
    }

    [Fact]
    public void Field_navigation_with_header()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Company, c => c.Name, f => f.Header("Société"));

        builder.Fields[0].Header.ShouldBe("Société");
        builder.Fields[0].IsNavigation.ShouldBeTrue();
    }

    // ---- IncludeId / IncludeBusinessKey ------------------------------

    [Fact]
    public void IncludeId_sets_flag()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.IncludeId();

        builder.IncludeIdFlag.ShouldBeTrue();
    }

    [Fact]
    public void IncludeBusinessKey_sets_flag()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.IncludeBusinessKey();

        builder.IncludeBusinessKeyFlag.ShouldBeTrue();
    }

    // ---- Fluent chaining ---------------------------------------------

    [Fact]
    public void Fluent_chaining_builds_multiple_fields()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder
            .IncludeId()
            .IncludeBusinessKey()
            .Field(e => e.Name, f => f.Header("Nom"))
            .Field(e => e.Email)
            .Field(e => e.BirthDate, f => f.Header("Date de naissance").Format("dd/MM/yyyy"))
            .Field(e => e.Company, c => c.Name, f => f.Header("Société"));

        builder.Fields.Count.ShouldBe(4);
        builder.IncludeIdFlag.ShouldBeTrue();
        builder.IncludeBusinessKeyFlag.ShouldBeTrue();
        builder.Fields[0].PropertyPath.ShouldBe("Name");
        builder.Fields[1].PropertyPath.ShouldBe("Email");
        builder.Fields[2].PropertyPath.ShouldBe("BirthDate");
        builder.Fields[3].PropertyPath.ShouldBe("Company.Name");
    }

    // ---- ExportDefinition integration --------------------------------

    [Fact]
    public void ExportDefinition_GetFields_returns_configured_fields()
    {
        TestExportDefinition definition = new();

        IReadOnlyList<ExportFieldDescriptor> fields = definition.GetFields();

        fields.Count.ShouldBe(3);
        fields[0].PropertyPath.ShouldBe("Name");
        fields[1].PropertyPath.ShouldBe("Email");
        fields[2].PropertyPath.ShouldBe("Company.Name");
    }

    [Fact]
    public void ExportDefinition_properties()
    {
        TestExportDefinition definition = new();

        definition.Name.ShouldBe("Test.Export");
        definition.EntityType.ShouldBe(typeof(TestEntity));
        definition.QueryDefinitionName.ShouldBeNull();
        definition.SupportedFormats.ShouldBe(["xlsx", "csv"]);
    }

    // ---- ExportDefinition GetIncludeId / GetIncludeBusinessKey --------

    [Fact]
    public void ExportDefinition_GetIncludeId_returns_false_by_default()
    {
        TestExportDefinition definition = new();

        definition.GetIncludeId().ShouldBeFalse();
    }

    [Fact]
    public void ExportDefinition_GetIncludeId_returns_true_when_configured()
    {
        ExportDefinitionWithId definition = new();

        definition.GetIncludeId().ShouldBeTrue();
    }

    [Fact]
    public void ExportDefinition_GetIncludeBusinessKey_returns_false_by_default()
    {
        TestExportDefinition definition = new();

        definition.GetIncludeBusinessKey().ShouldBeFalse();
    }

    [Fact]
    public void ExportDefinition_GetIncludeBusinessKey_returns_true_when_configured()
    {
        ExportDefinitionWithId definition = new();

        definition.GetIncludeBusinessKey().ShouldBeTrue();
    }

    // ---- ExportDefinition QueryDefinitionName -----------------------

    [Fact]
    public void ExportDefinition_QueryDefinitionName_returns_value_when_set()
    {
        QueryExportDefinition definition = new();

        definition.QueryDefinitionName.ShouldBe("Test.Entities");
    }

    [Fact]
    public void ExportDefinition_SupportedFormats_can_be_overridden()
    {
        CustomFormatsDefinition definition = new();

        definition.SupportedFormats.ShouldBe(["csv"]);
    }

    // ---- GetBuilder caching -----------------------------------------

    [Fact]
    public void GetBuilder_returns_same_instance_on_subsequent_calls()
    {
        TestExportDefinition definition = new();

        IReadOnlyList<ExportFieldDescriptor> fields1 = definition.GetFields();
        IReadOnlyList<ExportFieldDescriptor> fields2 = definition.GetFields();

        fields1.Count.ShouldBe(fields2.Count);
        fields1.ShouldBe(fields2);
    }

    // ---- Empty definition -------------------------------------------

    [Fact]
    public void ExportDefinition_with_no_fields_returns_empty_list()
    {
        EmptyExportDefinition definition = new();

        definition.GetFields().ShouldBeEmpty();
    }

    // ---- Test helpers ------------------------------------------------

    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateOnly? BirthDate { get; set; }
        public TestCompany? Company { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class TestCompany
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.Export";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name, f => f.Header("Nom"))
                .Field(e => e.Email)
                .Field(e => e.Company, c => c.Name, f => f.Header("Société"));
    }

    private sealed class ExportDefinitionWithId : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.ExportWithId";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .IncludeId()
                .IncludeBusinessKey()
                .Field(e => e.Name);
    }

    private sealed class QueryExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.QueryExport";
        public override string? QueryDefinitionName => "Test.Entities";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder.Field(e => e.Name);
    }

    private sealed class CustomFormatsDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.CsvOnly";
        public override IReadOnlyList<string> SupportedFormats => ["csv"];

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder.Field(e => e.Name);
    }

    private sealed class EmptyExportDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.Empty";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder)
        {
            // No fields configured
        }
    }

    // ---- ComplexField ---------------------------------------------------

    [Fact]
    public void ComplexField_adds_descriptor_with_RequiresHierarchy_true()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.ComplexField("Tags", e => e.Tags);

        ExportFieldDescriptor field = builder.Fields.Single();
        field.PropertyPath.ShouldBe("Tags");
        field.RequiresHierarchy.ShouldBeTrue();
        field.IsNavigation.ShouldBeFalse();
    }

    [Fact]
    public void ComplexField_stores_ValueSelector_and_SelectorType()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.ComplexField("Tags", e => e.Tags);

        ExportFieldDescriptor field = builder.Fields.Single();
        field.ValueSelector.ShouldNotBeNull();
        field.SelectorType.ShouldBe(typeof(List<string>));
    }

    [Fact]
    public void ComplexField_ValueSelector_invoked_with_entity_returns_value()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();
        builder.ComplexField("Tags", e => e.Tags);

        TestEntity entity = new() { Tags = ["dotnet", "export"] };
        object? result = builder.Fields[0].ValueSelector!(entity);

        result.ShouldBe(entity.Tags);
    }

    [Fact]
    public void ComplexField_with_header_and_order()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.ComplexField("Tags", e => e.Tags, f => f.Header("Tag List").Order(5));

        ExportFieldDescriptor field = builder.Fields.Single();
        field.Header.ShouldBe("Tag List");
        field.Order.ShouldBe(5);
    }

    [Fact]
    public void ComplexField_empty_name_throws()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.ComplexField(string.Empty, e => e.Tags));
    }

    [Fact]
    public void ComplexField_null_selector_throws()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentNullException>(() =>
            builder.ComplexField<List<string>>("Tags", null!));
    }

    [Fact]
    public void ComplexField_autoorder_increments()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Name);             // order 0
        builder.ComplexField("Tags", e => e.Tags); // order 1

        builder.Fields[0].Order.ShouldBe(0);
        builder.Fields[1].Order.ShouldBe(1);
    }

    // ---- HasComplexFields / OnIncompatibleField -------------------------

    [Fact]
    public void HasComplexFields_false_when_no_complex_fields()
    {
        TestExportDefinition definition = new();

        ((IExportDefinitionDescriptor)definition).HasComplexFields.ShouldBeFalse();
    }

    [Fact]
    public void HasComplexFields_true_when_complex_field_present()
    {
        ComplexDefinition definition = new();

        ((IExportDefinitionDescriptor)definition).HasComplexFields.ShouldBeTrue();
    }

    [Fact]
    public void OnIncompatibleField_default_is_Throw()
    {
        TestExportDefinition definition = new();

        definition.OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Throw);
    }

    [Fact]
    public void OnIncompatibleField_override_is_respected()
    {
        SkipComplexDefinition definition = new();

        definition.OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Skip);
        ((IExportDefinitionDescriptor)definition).OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Skip);
    }

    // ---- Extended test helpers ------------------------------------------

    private sealed class ComplexDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.Complex";

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder
                .Field(e => e.Name)
                .ComplexField("Tags", e => e.Tags);
    }

    private sealed class SkipComplexDefinition : ExportDefinition<TestEntity>
    {
        public override string Name => "Test.SkipComplex";
        public override OnIncompatibleFieldPolicy OnIncompatibleField => OnIncompatibleFieldPolicy.Skip;

        protected override void Configure(ExportDefinitionBuilder<TestEntity> builder) =>
            builder.ComplexField("Tags", e => e.Tags);
    }

}
