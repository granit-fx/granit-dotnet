namespace Granit.Documents.Endpoints.Shares.Dtos;

/// <summary>
/// Wire-shape response wrapping the list of <see cref="ShareResponse"/> entries.
/// </summary>
public sealed record ListSharesResponse(IReadOnlyList<ShareResponse> Items);
