using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryRequestSanitizerTests
{
    private static QueryMetadata CreateMetadata() =>
        new()
        {
            Columns = [],
            FilterableFields =
            [
                new FilterableField("status", "String", [FilterOperator.Eq, FilterOperator.In], null, null),
                new FilterableField("amount", "Decimal", [FilterOperator.Gte, FilterOperator.Lte], null, null),
            ],
            SortableFields = [new SortableField("createdAt"), new SortableField("amount")],
            PresetFilterGroups = [],
            QuickFilters = [new QuickFilterMeta("mine", "Mine", false)],
            DateFilters = [],
            GroupByFields = [new GroupByField("status", "String")],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
        };

    [Fact]
    public void Whitelisted_filters_pass_and_unknown_keys_are_reported()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate
            {
                Filter =
                [
                    new("status.eq", "Active"),
                    new("status.contains", "Act"),
                    new("secret.eq", "x"),
                ],
            },
            CreateMetadata());

        result.Request.Filter.ShouldNotBeNull();
        result.Request.Filter!.Keys.ShouldBe(["status.eq"]);
        result.IgnoredFilters.ShouldBe(["status.contains", "secret.eq"]);
    }

    [Fact]
    public void Duplicate_filter_keys_collapse_first_wins()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate
            {
                Filter = [new("status.eq", "Active"), new("STATUS.EQ", "Inactive")],
            },
            CreateMetadata());

        result.Request.Filter!["status.eq"].ShouldBe("Active");
        result.Request.Filter!.Count.ShouldBe(1);
    }

    [Fact]
    public void Sort_segments_are_stripped_to_the_sortable_whitelist()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate { Sort = "-createdAt, hacked, amount" },
            CreateMetadata());

        result.Request.Sort.ShouldBe("-createdAt,amount");
    }

    [Fact]
    public void Fully_invalid_sort_group_and_quick_filters_become_null()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate
            {
                Sort = "hacked",
                GroupBy = "hacked",
                QuickFilters = ["hacked"],
            },
            CreateMetadata());

        result.Request.Sort.ShouldBeNull();
        result.Request.GroupBy.ShouldBeNull();
        result.Request.QuickFilters.ShouldBeNull();
    }

    [Fact]
    public void PageSize_is_clamped_to_the_definition_max_and_page_floored()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate { Page = -3, PageSize = 1_000_000 },
            CreateMetadata());

        result.Request.Page.ShouldBeNull();
        result.Request.PageSize.ShouldBe(100);
    }

    [Fact]
    public void Null_pagination_stays_null_so_the_engine_applies_its_defaults()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate(),
            CreateMetadata());

        result.Request.Page.ShouldBeNull();
        result.Request.PageSize.ShouldBeNull();
        result.IgnoredFilters.ShouldBeEmpty();
    }

    [Fact]
    public void Whitelisted_group_by_and_quick_filters_pass_through()
    {
        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(
            new QueryRequestCandidate { GroupBy = "status", QuickFilters = ["mine"] },
            CreateMetadata());

        result.Request.GroupBy.ShouldBe("status");
        result.Request.QuickFilters.ShouldBe(["mine"]);
    }

    [Fact]
    public void OperatorCode_is_the_lowercase_enum_name()
    {
        QueryRequestSanitizer.OperatorCode(FilterOperator.Gte).ShouldBe("gte");
        QueryRequestSanitizer.OperatorCode(FilterOperator.Contains).ShouldBe("contains");
    }
}
