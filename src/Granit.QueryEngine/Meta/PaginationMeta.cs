namespace Granit.QueryEngine.Meta;

/// <summary>
/// Pagination metadata for frontend auto-configuration.
/// </summary>
/// <param name="DefaultPageSize">The default page size.</param>
/// <param name="MaxPageSize">The maximum allowed page size.</param>
/// <param name="MaxStreamSize">The maximum number of items returned by streaming queries.</param>
/// <param name="SupportsCursor">Whether keyset/cursor pagination is supported.</param>
public sealed record PaginationMeta(
    int DefaultPageSize,
    int MaxPageSize,
    int MaxStreamSize,
    bool SupportsCursor);
