using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportOptionsTests
{
    [Fact]
    public void SectionName_IsDataExchangeExport() =>
        ExportOptions.SectionName.ShouldBe("DataExchange:Export");
}
