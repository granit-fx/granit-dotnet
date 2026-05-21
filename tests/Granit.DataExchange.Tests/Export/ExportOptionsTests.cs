using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportOptionsTests
{
    [Fact]
    public void SectionName_IsDataExchangeExport() =>
        ExportOptions.SectionName.ShouldBe("DataExchange:Export");

    [Fact]
    public void DefaultBackgroundThreshold_Is1000()
    {
        ExportOptions options = new();

        options.BackgroundThreshold.ShouldBe(1000);
    }

    [Fact]
    public void BackgroundThreshold_CanBeSet()
    {
        ExportOptions options = new() { BackgroundThreshold = 500 };

        options.BackgroundThreshold.ShouldBe(500);
    }
}
