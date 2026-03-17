namespace Granit.Features.Endpoints.Dtos;

/// <summary>
/// Response for a single resolved feature value.
/// </summary>
/// <param name="Name">Feature name.</param>
/// <param name="Value">Resolved value for the current context.</param>
public sealed record FeatureValueResponse(string Name, string Value);
