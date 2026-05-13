using Granit.IO.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class IOActivitySourceTests
{
    [Fact]
    public void Name_Matches_MeterName() =>
        IOActivitySource.Name.ShouldBe("Granit.IO");

    [Fact]
    public void Instance_HasMatchingName() =>
        IOActivitySource.Instance.Name.ShouldBe(IOActivitySource.Name);
}
