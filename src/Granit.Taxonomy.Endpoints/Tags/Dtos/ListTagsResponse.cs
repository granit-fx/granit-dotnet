namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>Wire-shape response for the per-scope autocomplete listing.</summary>
public sealed record ListTagsResponse(IReadOnlyList<TagResponse> Items);
