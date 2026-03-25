using Granit.QueryEngine.Meta;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Meta;

public sealed class QueryMetadataTests
{
    [Fact]
    public void All_required_properties_are_set()
    {
        QueryMetadata metadata = new()
        {
            Columns = [new ColumnDefinition("Name", "Nom", "String", 1, true, true, true, null)],
            FilterableFields = [new FilterableField("Name", "String", [Granit.QueryEngine.Filtering.FilterOperator.Eq])],
            SortableFields = [new SortableField("Name")],
            PresetFilterGroups = [new FilterGroupMeta("Status", "Statut", [new PresetMeta("Active", "Actif", true)])],
            QuickFilters = [new QuickFilterMeta("MyItems", "Mes éléments", true)],
            DateFilters = [new DateFilterMeta("CreatedAt", DatePeriod.ThisMonth, [DatePeriod.Today, DatePeriod.ThisMonth, DatePeriod.ThisYear])],
            GroupByFields = [new GroupByField("Status", "String")],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, true),
            DefaultSort = "-createdAt",
        };

        metadata.Columns.Count.ShouldBe(1);
        metadata.FilterableFields.Count.ShouldBe(1);
        metadata.SortableFields.Count.ShouldBe(1);
        metadata.PresetFilterGroups.Count.ShouldBe(1);
        metadata.DateFilters.Count.ShouldBe(1);
        metadata.GroupByFields.Count.ShouldBe(1);
        metadata.Pagination.DefaultPageSize.ShouldBe(20);
        metadata.Pagination.MaxPageSize.ShouldBe(100);
        metadata.Pagination.SupportsCursor.ShouldBeTrue();
        metadata.DefaultSort.ShouldBe("-createdAt");
    }

    [Fact]
    public void ColumnDefinition_properties()
    {
        ColumnDefinition column = new("LastName", "Nom de famille", "String", 2, true, true, true, null);

        column.Name.ShouldBe("LastName");
        column.Label.ShouldBe("Nom de famille");
        column.Type.ShouldBe("String");
        column.Order.ShouldBe(2);
        column.IsSortable.ShouldBeTrue();
        column.IsFilterable.ShouldBeTrue();
        column.IsVisible.ShouldBeTrue();
        column.Format.ShouldBeNull();
    }

    [Fact]
    public void FilterableField_properties()
    {
        FilterableField field = new("Age", "Int32",
            [Granit.QueryEngine.Filtering.FilterOperator.Eq, Granit.QueryEngine.Filtering.FilterOperator.Gt, Granit.QueryEngine.Filtering.FilterOperator.Lt]);

        field.Name.ShouldBe("Age");
        field.Type.ShouldBe("Int32");
        field.Operators.Count.ShouldBe(3);
    }

    [Fact]
    public void FilterGroupMeta_with_presets()
    {
        FilterGroupMeta group = new("Status", "Statut",
            [new PresetMeta("Active", "Actif", true), new PresetMeta("Archived", "Archivé", false)]);

        group.Name.ShouldBe("Status");
        group.Label.ShouldBe("Statut");
        group.Presets.Count.ShouldBe(2);
        group.Presets[0].IsDefault.ShouldBeTrue();
        group.Presets[1].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void DateFilterMeta_properties()
    {
        DateFilterMeta filter = new("CreatedAt", DatePeriod.ThisMonth,
            [DatePeriod.Today, DatePeriod.ThisWeek, DatePeriod.ThisMonth]);

        filter.Name.ShouldBe("CreatedAt");
        filter.DefaultPeriod.ShouldBe(DatePeriod.ThisMonth);
        filter.AvailablePeriods.Count.ShouldBe(3);
    }

    [Fact]
    public void PaginationMeta_properties()
    {
        PaginationMeta pagination = new(25, 200, 50_000, false);

        pagination.DefaultPageSize.ShouldBe(25);
        pagination.MaxPageSize.ShouldBe(200);
        pagination.MaxStreamSize.ShouldBe(50_000);
        pagination.SupportsCursor.ShouldBeFalse();
    }

    [Fact]
    public void QuickFilterMeta_properties()
    {
        QuickFilterMeta filter = new("MyAppointments", "Mes rendez-vous", true);

        filter.Name.ShouldBe("MyAppointments");
        filter.Label.ShouldBe("Mes rendez-vous");
        filter.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void DefaultSort_can_be_null()
    {
        QueryMetadata metadata = new()
        {
            Columns = [],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
        };

        metadata.DefaultSort.ShouldBeNull();
    }
}
