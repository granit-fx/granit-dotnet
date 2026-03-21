using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Keys;

public sealed class DocumentFormatTests
{
    [Fact]
    public void Html_HasValue0() => ((int)DocumentFormat.Html).ShouldBe(0);

    [Fact]
    public void Pdf_HasValue1() => ((int)DocumentFormat.Pdf).ShouldBe(1);

    [Fact]
    public void Excel_HasValue2() => ((int)DocumentFormat.Excel).ShouldBe(2);

    [Fact]
    public void AllValues_AreDistinct()
    {
        DocumentFormat[] values = Enum.GetValues<DocumentFormat>();

        values.Length.ShouldBe(3);
        values.Distinct().Count().ShouldBe(3);
    }
}
