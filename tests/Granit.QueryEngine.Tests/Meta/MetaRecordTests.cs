// =============================================================================
// MetaRecordTests - Record equality and construction for meta types
// =============================================================================
// Verifies:
//   - SortableField record construction and equality
//   - GroupByField record construction and equality
//   - QuickFilterMeta record equality
//   - PresetMeta record equality
//   - FilterGroupMeta record equality
//   - PaginationMeta record equality
//   - ColumnDefinition record equality
//   - FilterableField record equality
//   - DateFilterMeta record equality
// =============================================================================

using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Meta;

public sealed class SortableFieldTests
{
    [Fact]
    public void Name_IsPreserved()
    {
        SortableField field = new("LastName");

        field.Name.ShouldBe("LastName");
    }

    [Fact]
    public void Equality_SameName_AreEqual()
    {
        SortableField a = new("CreatedAt");
        SortableField b = new("CreatedAt");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        SortableField a = new("CreatedAt");
        SortableField b = new("UpdatedAt");

        a.ShouldNotBe(b);
    }
}

public sealed class GroupByFieldTests
{
    [Fact]
    public void Properties_ArePreserved()
    {
        GroupByField field = new("Status", "String");

        field.Name.ShouldBe("Status");
        field.Type.ShouldBe("String");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        GroupByField a = new("Status", "String");
        GroupByField b = new("Status", "String");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        GroupByField a = new("Status", "String");
        GroupByField b = new("Category", "String");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentType_AreNotEqual()
    {
        GroupByField a = new("Status", "String");
        GroupByField b = new("Status", "Int32");

        a.ShouldNotBe(b);
    }
}

public sealed class QuickFilterMetaEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        QuickFilterMeta a = new("MyItems", "Mes items", true);
        QuickFilterMeta b = new("MyItems", "Mes items", true);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        QuickFilterMeta a = new("MyItems", "Mes items", true);
        QuickFilterMeta b = new("AllItems", "Mes items", true);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentIsDefault_AreNotEqual()
    {
        QuickFilterMeta a = new("MyItems", "Mes items", true);
        QuickFilterMeta b = new("MyItems", "Mes items", false);

        a.ShouldNotBe(b);
    }
}

public sealed class PresetMetaEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PresetMeta a = new("Active", "Actif", true);
        PresetMeta b = new("Active", "Actif", true);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        PresetMeta a = new("Active", "Actif", true);
        PresetMeta b = new("Archived", "Actif", true);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentLabel_AreNotEqual()
    {
        PresetMeta a = new("Active", "Actif", true);
        PresetMeta b = new("Active", "Active", true);

        a.ShouldNotBe(b);
    }
}

public sealed class FilterGroupMetaEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyList<PresetMeta> presets = [new PresetMeta("Active", "Actif", true)];
        FilterGroupMeta a = new("Status", "Statut", presets);
        FilterGroupMeta b = new("Status", "Statut", presets);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        IReadOnlyList<PresetMeta> presets = [new PresetMeta("Active", "Actif", true)];
        FilterGroupMeta a = new("Status", "Statut", presets);
        FilterGroupMeta b = new("Category", "Statut", presets);

        a.ShouldNotBe(b);
    }
}

public sealed class PaginationMetaEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PaginationMeta a = new(20, 100, 50_000, true);
        PaginationMeta b = new(20, 100, 50_000, true);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentDefaultPageSize_AreNotEqual()
    {
        PaginationMeta a = new(20, 100, 50_000, true);
        PaginationMeta b = new(25, 100, 50_000, true);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentMaxStreamSize_AreNotEqual()
    {
        PaginationMeta a = new(20, 100, 50_000, true);
        PaginationMeta b = new(20, 100, 100_000, true);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentSupportsCursor_AreNotEqual()
    {
        PaginationMeta a = new(20, 100, 50_000, true);
        PaginationMeta b = new(20, 100, 50_000, false);

        a.ShouldNotBe(b);
    }
}

public sealed class ColumnDefinitionEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        ColumnDefinition a = new("Name", "Nom", "String", 1, true, true, true, null);
        ColumnDefinition b = new("Name", "Nom", "String", 1, true, true, true, null);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        ColumnDefinition a = new("Name", "Nom", "String", 1, true, true, true, null);
        ColumnDefinition b = new("Email", "Nom", "String", 1, true, true, true, null);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentFormat_AreNotEqual()
    {
        ColumnDefinition a = new("Date", "Date", "DateTimeOffset", 1, true, true, true, "dd/MM/yyyy");
        ColumnDefinition b = new("Date", "Date", "DateTimeOffset", 1, true, true, true, "yyyy-MM-dd");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_NullFormat_VsNonNull_AreNotEqual()
    {
        ColumnDefinition a = new("Date", "Date", "DateTimeOffset", 1, true, true, true, null);
        ColumnDefinition b = new("Date", "Date", "DateTimeOffset", 1, true, true, true, "dd/MM/yyyy");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void ValueKind_And_CurrencyCode_DefaultToNull()
    {
        ColumnDefinition column = new("Name", "Nom", "String", 1, true, true, true, null);

        column.ValueKind.ShouldBeNull();
        column.CurrencyCode.ShouldBeNull();
    }

    [Fact]
    public void ValueKind_And_CurrencyCode_CanBeSet()
    {
        ColumnDefinition column = new(
            "Amount", "Montant", "Decimal", 1, true, true, true, null,
            ValueKind.Currency, "EUR");

        column.ValueKind.ShouldBe(ValueKind.Currency);
        column.CurrencyCode.ShouldBe("EUR");
    }

    [Fact]
    public void Equality_DifferentValueKind_AreNotEqual()
    {
        ColumnDefinition a = new("Amount", "Montant", "Decimal", 1, true, true, true, null, ValueKind.Currency, "EUR");
        ColumnDefinition b = new("Amount", "Montant", "Decimal", 1, true, true, true, null, ValueKind.Percentage);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void CurrencyCodeField_DefaultsToNull()
    {
        ColumnDefinition column = new("Amount", "Montant", "Decimal", 1, true, true, true, null, ValueKind.Currency);

        column.CurrencyCodeField.ShouldBeNull();
    }

    [Fact]
    public void CurrencyCodeField_CanBeSet()
    {
        ColumnDefinition column = new(
            "Amount", "Montant", "Decimal", 1, true, true, true, null,
            ValueKind.Currency, CurrencyCode: null, CurrencyCodeField: "Currency");

        column.CurrencyCodeField.ShouldBe("Currency");
        column.CurrencyCode.ShouldBeNull();
    }
}

public sealed class FilterableFieldEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyList<FilterOperator> operators = [FilterOperator.Eq, FilterOperator.Contains];
        FilterableField a = new("Name", "String", operators);
        FilterableField b = new("Name", "String", operators);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        IReadOnlyList<FilterOperator> operators = [FilterOperator.Eq];
        FilterableField a = new("Name", "String", operators);
        FilterableField b = new("Email", "String", operators);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Lookup_defaults_to_null()
    {
        IReadOnlyList<FilterOperator> operators = [FilterOperator.Eq];
        FilterableField field = new("Name", "String", operators);

        field.Lookup.ShouldBeNull();
    }

    [Fact]
    public void Equality_different_lookup_are_not_equal()
    {
        IReadOnlyList<FilterOperator> operators = [FilterOperator.Eq];
        FilterableField a = new("TenantId", "Guid", operators,
            Lookup: new Granit.DataLookup.Descriptors.LookupDescriptor(Name: "tenants"));
        FilterableField b = new("TenantId", "Guid", operators,
            Lookup: new Granit.DataLookup.Descriptors.LookupDescriptor(Name: "organizations"));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_same_lookup_are_equal()
    {
        IReadOnlyList<FilterOperator> operators = [FilterOperator.Eq];
        Granit.DataLookup.Descriptors.LookupDescriptor lookup = new(Name: "tenants");
        FilterableField a = new("TenantId", "Guid", operators, Lookup: lookup);
        FilterableField b = new("TenantId", "Guid", operators, Lookup: lookup);

        a.ShouldBe(b);
    }
}

public sealed class DateFilterMetaEqualityTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyList<DatePeriod> periods = [DatePeriod.Today, DatePeriod.ThisMonth];
        DateFilterMeta a = new("CreatedAt", DatePeriod.ThisMonth, periods);
        DateFilterMeta b = new("CreatedAt", DatePeriod.ThisMonth, periods);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentDefaultPeriod_AreNotEqual()
    {
        IReadOnlyList<DatePeriod> periods = [DatePeriod.Today, DatePeriod.ThisMonth];
        DateFilterMeta a = new("CreatedAt", DatePeriod.ThisMonth, periods);
        DateFilterMeta b = new("CreatedAt", DatePeriod.ThisYear, periods);

        a.ShouldNotBe(b);
    }
}
