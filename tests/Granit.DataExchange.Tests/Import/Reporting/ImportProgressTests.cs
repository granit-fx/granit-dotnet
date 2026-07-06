using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Reporting;

public sealed class ImportProgressTests
{
    [Fact]
    public void Constructor_ZeroValues()
    {
        var sut = new ImportProgress(0, 0, 0, 0);

        sut.ProcessedRows.ShouldBe(0);
        sut.TotalRows.ShouldBe(0);
        sut.SucceededRows.ShouldBe(0);
        sut.FailedRows.ShouldBe(0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ImportProgress(10, 20, 8, 2);
        var b = new ImportProgress(10, 20, 8, 2);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new ImportProgress(10, 20, 8, 2);
        var b = new ImportProgress(15, 20, 12, 3);

        a.ShouldNotBe(b);
    }
}
