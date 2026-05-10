using Granit.IO.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class IoActivitySourceTests
{
    [Fact]
    public void Name_Matches_MeterName() =>
        IoActivitySource.Name.ShouldBe("Granit.IO");

    [Fact]
    public void Instance_HasMatchingName() =>
        IoActivitySource.Instance.Name.ShouldBe(IoActivitySource.Name);
}
