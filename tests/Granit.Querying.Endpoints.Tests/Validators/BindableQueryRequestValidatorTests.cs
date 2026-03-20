using FluentValidation.Results;
using Granit.Querying.Endpoints.Dtos;
using Granit.Querying.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Querying.Endpoints.Tests.Validators;

public sealed class BindableQueryRequestValidatorTests
{
    private readonly BindableQueryRequestValidator _validator = new();

    private static BindableQueryRequest Create(QueryRequest request) =>
        BindableQueryRequest.FromQueryRequest(request);

    [Fact]
    public void Default_query_request_passes_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest());

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_paginated_request_passes_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Page = 1,
            PageSize = 25,
            Search = "test",
            Sort = "-createdAt,name",
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Page_below_minimum_fails_validation(int page)
    {
        BindableQueryRequest request = Create(new QueryRequest { Page = page });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Page");
    }

    [Fact]
    public void Page_at_minimum_passes_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Page = BindableQueryRequestValidator.MinPage,
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageSize_below_minimum_fails_validation(int pageSize)
    {
        BindableQueryRequest request = Create(new QueryRequest { PageSize = pageSize });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.PageSize");
    }

    [Fact]
    public void PageSize_exceeding_maximum_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            PageSize = BindableQueryRequestValidator.MaxPageSize + 1,
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.PageSize");
    }

    [Fact]
    public void PageSize_at_maximum_passes_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            PageSize = BindableQueryRequestValidator.MaxPageSize,
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Cursor_and_page_together_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Page = 2,
            Cursor = "abc123",
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Cursor");
    }

    [Fact]
    public void Cursor_without_page_passes_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Cursor = "abc123",
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Cursor_exceeding_max_length_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Cursor = new string('x', BindableQueryRequestValidator.MaxCursorLength + 1),
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Cursor");
    }

    [Fact]
    public void Search_exceeding_max_length_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Search = new string('a', BindableQueryRequestValidator.MaxSearchLength + 1),
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Search");
    }

    [Fact]
    public void Sort_exceeding_max_length_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            Sort = new string('a', BindableQueryRequestValidator.MaxSortLength + 1),
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Sort");
    }

    [Fact]
    public void GroupBy_exceeding_max_length_fails_validation()
    {
        BindableQueryRequest request = Create(new QueryRequest
        {
            GroupBy = new string('a', BindableQueryRequestValidator.MaxGroupByLength + 1),
        });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.GroupBy");
    }

    [Fact]
    public void Filter_exceeding_max_entries_fails_validation()
    {
        var filters = new Dictionary<string, string>();
        for (int i = 0; i <= BindableQueryRequestValidator.MaxFilterEntries; i++)
        {
            filters[$"field{i}.eq"] = $"value{i}";
        }

        BindableQueryRequest request = Create(new QueryRequest { Filter = filters });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Filter");
    }

    [Fact]
    public void Filter_at_max_entries_passes_validation()
    {
        var filters = new Dictionary<string, string>();
        for (int i = 0; i < BindableQueryRequestValidator.MaxFilterEntries; i++)
        {
            filters[$"field{i}.eq"] = $"value{i}";
        }

        BindableQueryRequest request = Create(new QueryRequest { Filter = filters });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void QuickFilters_exceeding_max_entries_fails_validation()
    {
        var quickFilters = Enumerable
            .Range(0, BindableQueryRequestValidator.MaxQuickFilters + 1)
            .Select(i => $"filter{i}")
            .ToList();

        BindableQueryRequest request = Create(new QueryRequest { QuickFilters = quickFilters });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.QuickFilters");
    }

    [Fact]
    public void Presets_exceeding_max_entries_fails_validation()
    {
        var presets = new Dictionary<string, string>();
        for (int i = 0; i <= BindableQueryRequestValidator.MaxPresetEntries; i++)
        {
            presets[$"group{i}"] = $"preset{i}";
        }

        BindableQueryRequest request = Create(new QueryRequest { Presets = presets });

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Value.Presets");
    }
}
