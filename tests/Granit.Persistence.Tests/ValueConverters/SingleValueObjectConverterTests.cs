// =============================================================================
// Tests - SingleValueObjectConverter
// =============================================================================
// Verifie que le ValueConverter EF Core convertit correctement les
// SingleValueObject<T> vers leur primitif sous-jacent et inversement,
// incluant le round-trip et la reconstruction par reflexion.
// =============================================================================

using Granit.Domain;
using Granit.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.ValueConverters;

public sealed class SingleValueObjectConverterTests
{
    // ──── Test value object ────

    private sealed class TestCode : SingleValueObject<string>
    {
        public override required string Value { get; init; }

        public static TestCode Create(string value) => new() { Value = value };
    }

    private sealed class TestQuantity : SingleValueObject<int>
    {
        public override required int Value { get; init; }

        public static TestQuantity Create(int value) => new() { Value = value };
    }

    // ──── String-based converter ────

    private readonly ValueConverter<TestCode, string> _stringConverter = new SingleValueObjectConverter<TestCode, string>();

    [Fact]
    public void ConvertToProvider_String_ReturnsUnderlyingValue()
    {
        var code = TestCode.Create("ABC-123");

        string result = _stringConverter.ConvertToProviderTyped(code);

        result.ShouldBe("ABC-123");
    }

    [Fact]
    public void ConvertFromProvider_String_ReconstructsValueObject()
    {
        TestCode result = _stringConverter.ConvertFromProviderTyped("XYZ-789");

        result.ShouldNotBeNull();
        result.Value.ShouldBe("XYZ-789");
    }

    [Fact]
    public void RoundTrip_String_PreservesValue()
    {
        var original = TestCode.Create("ROUND-TRIP");

        string primitive = _stringConverter.ConvertToProviderTyped(original);
        TestCode restored = _stringConverter.ConvertFromProviderTyped(primitive);

        restored.Value.ShouldBe(original.Value);
    }

    // ──── Int-based converter ────

    private readonly ValueConverter<TestQuantity, int> _intConverter = new SingleValueObjectConverter<TestQuantity, int>();

    [Fact]
    public void ConvertToProvider_Int_ReturnsUnderlyingValue()
    {
        var quantity = TestQuantity.Create(42);

        int result = _intConverter.ConvertToProviderTyped(quantity);

        result.ShouldBe(42);
    }

    [Fact]
    public void ConvertFromProvider_Int_ReconstructsValueObject()
    {
        TestQuantity result = _intConverter.ConvertFromProviderTyped(99);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(99);
    }

    [Fact]
    public void RoundTrip_Int_PreservesValue()
    {
        var original = TestQuantity.Create(7);

        int primitive = _intConverter.ConvertToProviderTyped(original);
        TestQuantity restored = _intConverter.ConvertFromProviderTyped(primitive);

        restored.Value.ShouldBe(original.Value);
    }
}
