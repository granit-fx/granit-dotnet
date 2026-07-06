using Granit.DataExchange.Import.Grouping;
using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Grouping;

public sealed class GroupedRowsTests
{
    [Fact]
    public void Constructor_EmptyRows()
    {
        var sut = new GroupedRows("GRP-EMPTY", []);

        sut.GroupKeyValue.ShouldBe("GRP-EMPTY");
        sut.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        IReadOnlyList<RawImportRow> rows = [new RawImportRow(1, new Dictionary<string, string?> { ["A"] = "1" })];

        var a = new GroupedRows("KEY", rows);
        var b = new GroupedRows("KEY", rows);

        a.ShouldBe(b);
    }
}
