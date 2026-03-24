using Microsoft.AspNetCore.Mvc;

namespace Granit.Endpoints;

/// <summary>
/// Result of a batch operation where individual items may succeed or fail independently.
/// Returns HTTP 207 Multi-Status when at least one item failed.
/// </summary>
/// <typeparam name="T">The type of successful item values.</typeparam>
/// <param name="Results">Individual results for each item in the batch.</param>
/// <param name="SuccessCount">Number of items that succeeded.</param>
/// <param name="FailureCount">Number of items that failed.</param>
public sealed record BatchResult<T>(
    IReadOnlyList<BatchItemResult<T>> Results,
    int SuccessCount,
    int FailureCount);

/// <summary>
/// Result of a single item within a <see cref="BatchResult{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <param name="Value">The resulting value when successful; <c>null</c> on failure.</param>
/// <param name="IsSuccess">Whether this item was processed successfully.</param>
/// <param name="Error">
/// Problem details describing the failure; <c>null</c> on success.
/// Uses RFC 7807 format for consistency with single-item error responses.
/// </param>
public sealed record BatchItemResult<T>(
    T? Value,
    bool IsSuccess,
    ProblemDetails? Error);
