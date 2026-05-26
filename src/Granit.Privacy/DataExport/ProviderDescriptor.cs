namespace Granit.Privacy.DataExport;

/// <summary>
/// Public-facing metadata for a single privacy data provider, as surfaced by
/// <see cref="IPrivacyScopeResolver.ListVisibleAsync"/> to the scope selector
/// (<c>GET /privacy/exports/scopes</c>).
/// </summary>
/// <param name="ProviderName">Stable identifier — matches
/// <see cref="IPrivacyDataProvider.ProviderName"/>.</param>
/// <param name="DisplayKey">Localisation key for the human-readable label.
/// Resolved client-side or by the BFF.</param>
/// <param name="FeatureName">Optional feature flag controlling the scope. When non-null
/// the host can hide the scope via an <see cref="IPrivacyScopeVisibilityPolicy"/>
/// that consults <c>Granit.Features</c>.</param>
/// <param name="DefaultSelected">Whether the scope should be pre-checked in the UI.
/// Defaults to <see langword="true"/> — Takeout-style "everything by default".</param>
/// <param name="EstimatedSizeBytes">Optional best-effort size hint for the UI. Providers
/// that can answer cheaply (e.g. document count × average size) populate this; others
/// leave it <see langword="null"/>.</param>
public sealed record ProviderDescriptor(
    string ProviderName,
    string DisplayKey,
    string? FeatureName,
    bool DefaultSelected = true,
    long? EstimatedSizeBytes = null);
