namespace Granit.DataLookup.Endpoints.Dtos;

/// <summary>Response shape for a paginated lookup search.</summary>
/// <param name="Items">The page of items, in source order.</param>
/// <param name="TotalCount">Total count across all pages, or <see langword="null"/> for cursor-based sources.</param>
/// <param name="ContinuationToken">Opaque token for the next page, when supported.</param>
public sealed record LookupResultResponse(
    IReadOnlyList<LookupItemResponse> Items,
    int? TotalCount,
    string? ContinuationToken);
