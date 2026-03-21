using Granit.Querying.Endpoints.Dtos;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Querying.Endpoints.Tests.Dtos;

public sealed class BindableQueryRequestTests
{
    [Fact]
    public async Task BindAsync_returns_bindable_request()
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?page=2&pageSize=10");

        BindableQueryRequest? result = await BindableQueryRequest.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(10);
    }

    [Fact]
    public async Task BindAsync_empty_query_returns_default_request()
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString(string.Empty);

        BindableQueryRequest? result = await BindableQueryRequest.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Value.Page.ShouldBeNull();
        result.Value.PageSize.ShouldBeNull();
    }

    [Fact]
    public void FromQueryRequest_wraps_value()
    {
        QueryRequest request = new() { Page = 3, PageSize = 25 };

        var bindable = BindableQueryRequest.FromQueryRequest(request);

        bindable.Value.ShouldBe(request);
    }
}
