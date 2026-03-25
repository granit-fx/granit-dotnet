using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class QueryDefinitionTests
{
    private sealed class PatientQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Patients";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Nom").Sortable().Filterable())
                .Column(p => p.Email, c => c.Label("Email").Filterable())
                .GlobalSearch(p => p.Name, p => p.Email)
                .DefaultPageSize(25)
                .MaxPageSize(200)
                .SupportsCursorPagination(p => p.Id)
                .DefaultSort("-CreatedAt");
    }

    [Fact]
    public void Name_returns_declared_name()
    {
        PatientQueryDefinition definition = new();

        definition.Name.ShouldBe("Test.Patients");
    }

    [Fact]
    public void EntityType_returns_correct_type()
    {
        PatientQueryDefinition definition = new();

        definition.EntityType.ShouldBe(typeof(TestEntity));
    }

    [Fact]
    public void GetColumns_returns_declared_columns()
    {
        PatientQueryDefinition definition = new();

        IReadOnlyList<ColumnDescriptor> columns = definition.GetColumns();

        columns.Count.ShouldBe(2);
        columns[0].PropertyName.ShouldBe("Name");
        columns[0].Label.ShouldBe("Nom");
        columns[0].IsSortable.ShouldBeTrue();
        columns[0].IsFilterable.ShouldBeTrue();
        columns[1].PropertyName.ShouldBe("Email");
        columns[1].Label.ShouldBe("Email");
        columns[1].IsFilterable.ShouldBeTrue();
        columns[1].IsSortable.ShouldBeFalse();
    }

    [Fact]
    public void GetGlobalSearchProperties_returns_declared_properties()
    {
        PatientQueryDefinition definition = new();

        IReadOnlyList<string> properties = definition.GetGlobalSearchProperties();

        properties.ShouldBe(["Name", "Email"]);
    }

    [Fact]
    public void GetDefaultPageSize_returns_configured_value()
    {
        PatientQueryDefinition definition = new();

        definition.GetDefaultPageSize().ShouldBe(25);
    }

    [Fact]
    public void GetMaxPageSize_returns_configured_value()
    {
        PatientQueryDefinition definition = new();

        definition.GetMaxPageSize().ShouldBe(200);
    }

    [Fact]
    public void GetCursorProperty_returns_configured_property()
    {
        PatientQueryDefinition definition = new();

        definition.GetCursorProperty().ShouldBe("Id");
    }

    [Fact]
    public void GetDefaultSort_returns_configured_sort()
    {
        PatientQueryDefinition definition = new();

        definition.GetDefaultSort().ShouldBe("-CreatedAt");
    }

    [Fact]
    public void GetBuilder_is_lazily_initialized_and_cached()
    {
        PatientQueryDefinition definition = new();

        QueryDefinitionBuilder<TestEntity> first = definition.GetBuilder();
        QueryDefinitionBuilder<TestEntity> second = definition.GetBuilder();

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void Implements_IQueryDefinitionDescriptor()
    {
        PatientQueryDefinition definition = new();

        IQueryDefinitionDescriptor descriptor = definition;

        descriptor.Name.ShouldBe("Test.Patients");
        descriptor.EntityType.ShouldBe(typeof(TestEntity));
    }
}
