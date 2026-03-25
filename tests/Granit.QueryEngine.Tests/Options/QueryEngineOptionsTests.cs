using Granit.QueryEngine.Options;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Options;

public sealed class QueryEngineOptionsTests
{
    [Fact]
    public void DefaultPageSize_defaults_to_QueryEngineDefaults()
    {
        QueryEngineOptions options = new();

        options.DefaultPageSize.ShouldBe(QueryEngineDefaults.DefaultPageSize);
    }

    [Fact]
    public void MaxPageSize_defaults_to_QueryEngineDefaults()
    {
        QueryEngineOptions options = new();

        options.MaxPageSize.ShouldBe(QueryEngineDefaults.MaxPageSize);
    }

    [Fact]
    public void MaxStreamSize_defaults_to_QueryEngineDefaults()
    {
        QueryEngineOptions options = new();

        options.MaxStreamSize.ShouldBe(QueryEngineDefaults.MaxStreamSize);
    }

    [Fact]
    public void DefaultPageSize_can_be_set()
    {
        QueryEngineOptions options = new() { DefaultPageSize = 50 };

        options.DefaultPageSize.ShouldBe(50);
    }

    [Fact]
    public void MaxPageSize_can_be_set()
    {
        QueryEngineOptions options = new() { MaxPageSize = 500 };

        options.MaxPageSize.ShouldBe(500);
    }

    [Fact]
    public void MaxStreamSize_can_be_set()
    {
        QueryEngineOptions options = new() { MaxStreamSize = 200_000 };

        options.MaxStreamSize.ShouldBe(200_000);
    }
}
