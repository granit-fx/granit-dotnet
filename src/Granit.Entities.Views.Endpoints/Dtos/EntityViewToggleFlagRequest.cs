namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Request body for the boolean-toggle endpoints (pin / star / set-default).</summary>
/// <param name="Value">The desired flag value.</param>
public sealed record EntityViewToggleFlagRequest(bool Value);
