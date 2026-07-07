using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryEngineDefaultsTests
{
    [Fact]
    public void DefaultPageSize_is_20() => QueryEngineDefaults.DefaultPageSize.ShouldBe(20);

    [Fact]
    public void MaxPageSize_is_100() => QueryEngineDefaults.MaxPageSize.ShouldBe(100);

    [Fact]
    public void MaxStreamSize_is_100000() => QueryEngineDefaults.MaxStreamSize.ShouldBe(100_000);
}
