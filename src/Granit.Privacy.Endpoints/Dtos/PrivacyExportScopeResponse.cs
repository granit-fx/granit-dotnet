namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// One entry returned by <c>GET /privacy/exports/scopes</c> — describes a single
/// provider scope the data subject can include in an export request.
/// </summary>
/// <param name="ProviderName">Stable identifier (used in the POST body's
/// <c>Scopes</c> list).</param>
/// <param name="DisplayKey">Localisation key — the BFF / front resolves it.</param>
/// <param name="FeatureName">Optional feature flag tagging this scope (informational —
/// gating already applied server-side by the visibility policy).</param>
/// <param name="DefaultSelected">Whether the UI should pre-check this scope.</param>
/// <param name="EstimatedSizeBytes">Best-effort size hint, when the provider can answer
/// cheaply.</param>
public sealed record PrivacyExportScopeResponse(
    string ProviderName,
    string DisplayKey,
    string? FeatureName,
    bool DefaultSelected,
    long? EstimatedSizeBytes);
