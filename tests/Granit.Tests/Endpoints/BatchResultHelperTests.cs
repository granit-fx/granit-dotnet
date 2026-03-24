using Granit.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Xunit;

namespace Granit.Tests.Endpoints;

public sealed class BatchResultHelperTests
{
    [Fact]
    public void Create_counts_successes_and_failures()
    {
        List<BatchItemResult<string>> items =
        [
            BatchResultHelper.Success("a"),
            BatchResultHelper.Failure<string>("bad input"),
            BatchResultHelper.Success("c"),
        ];

        BatchResult<string> result = BatchResultHelper.Create(items);

        result.SuccessCount.ShouldBe(2);
        result.FailureCount.ShouldBe(1);
        result.Results.Count.ShouldBe(3);
    }

    [Fact]
    public void Success_item_has_value_and_no_error()
    {
        BatchItemResult<int> item = BatchResultHelper.Success(42);

        item.IsSuccess.ShouldBeTrue();
        item.Value.ShouldBe(42);
        item.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_item_has_error_and_no_value()
    {
        BatchItemResult<int> item = BatchResultHelper.Failure<int>("Invalid NISS");

        item.IsSuccess.ShouldBeFalse();
        item.Value.ShouldBe(default);
        item.Error.ShouldNotBeNull();
        item.Error.Detail.ShouldBe("Invalid NISS");
        item.Error.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void Failure_with_custom_status_code()
    {
        BatchItemResult<int> item = BatchResultHelper.Failure<int>(
            "Not found", StatusCodes.Status404NotFound);

        item.Error!.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToResult_returns_ok_when_all_succeed()
    {
        List<BatchItemResult<string>> items =
        [
            BatchResultHelper.Success("a"),
            BatchResultHelper.Success("b"),
        ];
        BatchResult<string> batch = BatchResultHelper.Create(items);

        IResult result = batch.ToResult();

        result.ShouldBeOfType<Ok<BatchResult<string>>>();
    }

    [Fact]
    public void ToResult_returns_207_when_any_fails()
    {
        List<BatchItemResult<string>> items =
        [
            BatchResultHelper.Success("a"),
            BatchResultHelper.Failure<string>("error"),
        ];
        BatchResult<string> batch = BatchResultHelper.Create(items);

        IResult result = batch.ToResult();

        result.ShouldBeOfType<JsonHttpResult<BatchResult<string>>>();
    }

    [Fact]
    public void Create_all_success_has_zero_failures()
    {
        List<BatchItemResult<string>> items =
        [
            BatchResultHelper.Success("a"),
        ];

        BatchResult<string> result = BatchResultHelper.Create(items);

        result.SuccessCount.ShouldBe(1);
        result.FailureCount.ShouldBe(0);
    }

    [Fact]
    public void Create_empty_list_has_zero_counts()
    {
        BatchResult<string> result = BatchResultHelper.Create<string>([]);

        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(0);
        result.Results.ShouldBeEmpty();
    }
}
