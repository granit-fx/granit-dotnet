using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Granit.Endpoints;

/// <summary>
/// Helper methods for building <see cref="BatchResult{T}"/> responses
/// and returning appropriate HTTP status codes (200 or 207).
/// </summary>
public static class BatchResultHelper
{
    /// <summary>
    /// Creates a <see cref="BatchResult{T}"/> from a list of individual results.
    /// </summary>
    public static BatchResult<T> Create<T>(IReadOnlyList<BatchItemResult<T>> results)
    {
        int successCount = 0;
        int failureCount = 0;

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].IsSuccess)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        return new BatchResult<T>(results, successCount, failureCount);
    }

    /// <summary>
    /// Creates a successful <see cref="BatchItemResult{T}"/>.
    /// </summary>
    public static BatchItemResult<T> Success<T>(T value) =>
        new(value, IsSuccess: true, Error: null);

    /// <summary>
    /// Creates a failed <see cref="BatchItemResult{T}"/> with RFC 7807 problem details.
    /// </summary>
    public static BatchItemResult<T> Failure<T>(string detail, int statusCode = StatusCodes.Status422UnprocessableEntity) =>
        new(Value: default, IsSuccess: false, Error: new ProblemDetails
        {
            Status = statusCode,
            Detail = detail,
        });

    /// <summary>
    /// Returns the batch result with the appropriate HTTP status code:
    /// <list type="bullet">
    ///   <item><c>200 OK</c> when all items succeeded</item>
    ///   <item><c>207 Multi-Status</c> when at least one item failed</item>
    /// </list>
    /// </summary>
    public static IResult ToResult<T>(this BatchResult<T> batch) =>
        batch.FailureCount > 0
            ? TypedResults.Json(batch, statusCode: StatusCodes.Status207MultiStatus)
            : TypedResults.Ok(batch);
}
