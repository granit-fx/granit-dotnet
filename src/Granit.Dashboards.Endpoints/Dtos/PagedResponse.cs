namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Generic pagination envelope used by the list endpoints. The frontend renders
/// pagination controls from <see cref="TotalCount"/> + <see cref="Page"/> +
/// <see cref="PageSize"/>; <see cref="Items"/> carries the page slice.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Page slice — at most <see cref="PageSize"/> items.</param>
/// <param name="TotalCount">Total number of items across all pages.</param>
/// <param name="Page">Zero-based current page index.</param>
/// <param name="PageSize">Number of items requested per page.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
