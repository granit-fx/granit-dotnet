using Granit.Querying.Options;
using Shouldly;
using Xunit;

namespace Granit.Querying.Tests.Options;

public sealed class QueryingOptionsTests
{
    [Fact]
    public void DefaultPageSize_defaults_to_QueryingDefaults()
    {
        QueryingOptions options = new();

        options.DefaultPageSize.ShouldBe(QueryingDefaults.DefaultPageSize);
    }

    [Fact]
    public void MaxPageSize_defaults_to_QueryingDefaults()
    {
        QueryingOptions options = new();

        options.MaxPageSize.ShouldBe(QueryingDefaults.MaxPageSize);
    }

    [Fact]
    public void MaxStreamSize_defaults_to_QueryingDefaults()
    {
        QueryingOptions options = new();

        options.MaxStreamSize.ShouldBe(QueryingDefaults.MaxStreamSize);
    }

    [Fact]
    public void DefaultPageSize_can_be_set()
    {
        QueryingOptions options = new() { DefaultPageSize = 50 };

        options.DefaultPageSize.ShouldBe(50);
    }

    [Fact]
    public void MaxPageSize_can_be_set()
    {
        QueryingOptions options = new() { MaxPageSize = 500 };

        options.MaxPageSize.ShouldBe(500);
    }

    [Fact]
    public void MaxStreamSize_can_be_set()
    {
        QueryingOptions options = new() { MaxStreamSize = 200_000 };

        options.MaxStreamSize.ShouldBe(200_000);
    }
}
