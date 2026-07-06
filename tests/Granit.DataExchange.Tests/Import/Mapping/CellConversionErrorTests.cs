using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class CellConversionErrorTests
{
    [Fact]
    public void RawValue_CanBeNull()
    {
        var sut = new CellConversionError(
            "Amount",
            "TotalAmount",
            null,
            "Decimal",
            "Granit:DataExchange:NullValue");

        sut.RawValue.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new CellConversionError("Col", "Prop", "val", "Int32", "ERR");
        var b = new CellConversionError("Col", "Prop", "val", "Int32", "ERR");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new CellConversionError("Col1", "Prop1", "v1", "Int32", "ERR1");
        var b = new CellConversionError("Col2", "Prop2", "v2", "String", "ERR2");

        a.ShouldNotBe(b);
    }
}
