namespace Granit.Features.Endpoints.Dtos;

/// <summary>
/// Response representing a group of related feature definitions.
/// </summary>
/// <param name="Name">Group name (e.g. <c>"Acme"</c>).</param>
/// <param name="DisplayName">Display label for admin UI, or <c>null</c>.</param>
/// <param name="Features">Feature definitions in this group.</param>
public sealed record FeatureGroupResponse(
    string Name,
    string? DisplayName,
    IReadOnlyList<FeatureDefinitionResponse> Features);
