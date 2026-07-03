// =============================================================================
// ColumnDescriptorTests - Immutable column metadata
// =============================================================================
// Verifies:
//   - Required and optional properties are preserved
//   - Default values for optional properties
//   - IsShadowProperty flag
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class ColumnDescriptorTests
{
    // ──── Construction & required properties ────

    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "LastName",
            ClrType = typeof(string),
        };

        descriptor.PropertyName.ShouldBe("LastName");
        descriptor.ClrType.ShouldBe(typeof(string));
    }

    // ──── Defaults ────

    [Fact]
    public void Label_DefaultsToNull()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.Label.ShouldBeNull();
    }

    [Fact]
    public void Order_DefaultsToZero()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.Order.ShouldBe(0);
    }

    [Fact]
    public void IsSortable_DefaultsToFalse()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.IsSortable.ShouldBeFalse();
    }

    [Fact]
    public void IsFilterable_DefaultsToFalse()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.IsFilterable.ShouldBeFalse();
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Format_DefaultsToNull()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.Format.ShouldBeNull();
    }

    [Fact]
    public void IsShadowProperty_DefaultsToFalse()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.IsShadowProperty.ShouldBeFalse();
    }

    [Fact]
    public void ValueKind_DefaultsToNull()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Name",
            ClrType = typeof(string),
        };

        descriptor.ValueKind.ShouldBeNull();
    }

    [Fact]
    public void ValueKind_CanBeSet()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            ValueKind = QueryEngine.ValueKind.Currency,
            CurrencyCode = "EUR",
        };

        descriptor.ValueKind.ShouldBe(QueryEngine.ValueKind.Currency);
        descriptor.CurrencyCode.ShouldBe("EUR");
    }

    // ──── Optional properties ────

    [Fact]
    public void AllOptionalProperties_CanBeSet()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "CreatedAt",
            ClrType = typeof(DateTimeOffset),
            Label = "Date de création",
            Order = 5,
            IsSortable = true,
            IsFilterable = true,
            IsVisible = false,
            Format = "dd/MM/yyyy",
            IsShadowProperty = true,
        };

        descriptor.Label.ShouldBe("Date de création");
        descriptor.Order.ShouldBe(5);
        descriptor.IsSortable.ShouldBeTrue();
        descriptor.IsFilterable.ShouldBeTrue();
        descriptor.IsVisible.ShouldBeFalse();
        descriptor.Format.ShouldBe("dd/MM/yyyy");
        descriptor.IsShadowProperty.ShouldBeTrue();
    }

    // ──── CLR type variations ────

    [Fact]
    public void ClrType_SupportsNumericTypes()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
        };

        descriptor.ClrType.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void ClrType_SupportsNullableTypes()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "BirthDate",
            ClrType = typeof(DateOnly?),
        };

        descriptor.ClrType.ShouldBe(typeof(DateOnly?));
    }

    [Fact]
    public void ClrType_SupportsGuid()
    {
        ColumnDescriptor descriptor = new()
        {
            PropertyName = "Id",
            ClrType = typeof(Guid),
        };

        descriptor.ClrType.ShouldBe(typeof(Guid));
    }
}
