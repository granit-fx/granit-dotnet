using Shouldly;
using Xunit;

namespace Granit.Querying.Tests;

public sealed class QueryingDefaultsTests
{
    [Fact]
    public void DefaultPageSize_is_20() => QueryingDefaults.DefaultPageSize.ShouldBe(20);

    [Fact]
    public void MaxPageSize_is_100() => QueryingDefaults.MaxPageSize.ShouldBe(100);

    [Fact]
    public void MaxStreamSize_is_100000() => QueryingDefaults.MaxStreamSize.ShouldBe(100_000);
}
