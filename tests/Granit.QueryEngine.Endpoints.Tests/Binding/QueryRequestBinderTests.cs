using Granit.QueryEngine.Endpoints.Binding;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests.Binding;

public sealed class QueryRequestBinderTests
{
    [Fact]
    public async Task BindAsync_returns_empty_request_when_no_params()
    {
        HttpContext context = CreateContext(string.Empty);

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Page.ShouldBeNull();
        result.PageSize.ShouldBeNull();
        result.Cursor.ShouldBeNull();
        result.Search.ShouldBeNull();
        result.Sort.ShouldBeNull();
        result.Filter.ShouldBeNull();
        result.Presets.ShouldBeNull();
        result.QuickFilters.ShouldBeNull();
        result.GroupBy.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsync_parses_scalar_params()
    {
        HttpContext context = CreateContext("?page=2&pageSize=25&search=laptop&sort=-price,name&groupBy=category");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(25);
        result.Search.ShouldBe("laptop");
        result.Sort.ShouldBe("-price,name");
        result.GroupBy.ShouldBe("category");
    }

    [Fact]
    public async Task BindAsync_parses_cursor()
    {
        HttpContext context = CreateContext("?cursor=abc123&pageSize=10");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Cursor.ShouldBe("abc123");
        result.PageSize.ShouldBe(10);
        result.Page.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsync_parses_filter_bracket_syntax()
    {
        HttpContext context = CreateContext("?filter[name.contains]=Alice&filter[age.gt]=18&filter[status.eq]=active");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(3);
        result.Filter["name.contains"].ShouldBe("Alice");
        result.Filter["age.gt"].ShouldBe("18");
        result.Filter["status.eq"].ShouldBe("active");
    }

    [Fact]
    public async Task BindAsync_parses_presets_bracket_syntax()
    {
        HttpContext context = CreateContext("?presets[category]=Electronics&presets[status]=active,pending");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Presets.ShouldNotBeNull();
        result.Presets.Count.ShouldBe(2);
        result.Presets["category"].ShouldBe("Electronics");
        result.Presets["status"].ShouldBe("active,pending");
    }

    [Fact]
    public async Task BindAsync_ignores_invalid_page_value()
    {
        HttpContext context = CreateContext("?page=abc&pageSize=xyz");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Page.ShouldBeNull();
        result.PageSize.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsync_ignores_empty_bracket_keys()
    {
        HttpContext context = CreateContext("?filter[]=value&filter[name.eq]=Alice");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(1);
        result.Filter["name.eq"].ShouldBe("Alice");
    }

    [Fact]
    public async Task BindAsync_bracket_keys_are_case_insensitive()
    {
        HttpContext context = CreateContext("?Filter[Name.eq]=Alice&PRESETS[Status]=active");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Filter.ShouldNotBeNull();
        result.Filter["Name.eq"].ShouldBe("Alice");
        result.Presets.ShouldNotBeNull();
        result.Presets["Status"].ShouldBe("active");
    }

    [Fact]
    public async Task BindAsync_parses_quick_filters()
    {
        DefaultHttpContext context = CreateContext("?quickFilters=MyAppointments,Unread");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.QuickFilters.ShouldNotBeNull();
        result.QuickFilters.Count.ShouldBe(2);
        result.QuickFilters[0].ShouldBe("MyAppointments");
        result.QuickFilters[1].ShouldBe("Unread");
    }

    [Fact]
    public async Task BindAsync_returns_null_quick_filters_when_absent()
    {
        DefaultHttpContext context = CreateContext("?page=1");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.QuickFilters.ShouldBeNull();
    }

    [Fact]
    public async Task BindAsync_parses_skipTotalCount_true()
    {
        HttpContext context = CreateContext("?page=1&pageSize=20&skipTotalCount=true");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.SkipTotalCount.ShouldBeTrue();
    }

    [Fact]
    public async Task BindAsync_parses_skipTotalCount_case_insensitive()
    {
        HttpContext context = CreateContext("?skipTotalCount=TRUE");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.SkipTotalCount.ShouldBeTrue();
    }

    [Fact]
    public async Task BindAsync_skipTotalCount_defaults_to_false_when_absent()
    {
        HttpContext context = CreateContext("?page=1");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.SkipTotalCount.ShouldBeFalse();
    }

    [Fact]
    public async Task BindAsync_skipTotalCount_false_when_not_true()
    {
        HttpContext context = CreateContext("?skipTotalCount=false");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.SkipTotalCount.ShouldBeFalse();
    }

    [Fact]
    public async Task BindAsync_full_complex_query_string()
    {
        HttpContext context = CreateContext(
            "?page=1&pageSize=20&search=test&sort=-createdAt" +
            "&filter[name.contains]=John&filter[price.gte]=100" +
            "&presets[category]=Electronics&groupBy=status");

        QueryRequest? result = await QueryRequestBinder.BindAsync(context, null!);

        result.ShouldNotBeNull();
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(20);
        result.Search.ShouldBe("test");
        result.Sort.ShouldBe("-createdAt");
        result.GroupBy.ShouldBe("status");
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(2);
        result.Presets.ShouldNotBeNull();
        result.Presets.Count.ShouldBe(1);
    }

    private static DefaultHttpContext CreateContext(string queryString)
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString(queryString);
        return context;
    }
}
