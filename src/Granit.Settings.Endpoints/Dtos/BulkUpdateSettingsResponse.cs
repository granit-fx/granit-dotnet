namespace Granit.Settings.Endpoints.Dtos;

/// <summary>
/// Per-entry outcomes from a bulk update. Returned with HTTP 200 regardless of individual
/// failures — the client inspects <see cref="BulkSettingResult.Outcome"/> to determine success.
/// </summary>
/// <param name="Results">One entry per item in the request, in the same order.</param>
public sealed record BulkUpdateSettingsResponse(IReadOnlyList<BulkSettingResult> Results);

/// <summary>
/// Outcome for a single entry in a bulk update.
/// </summary>
/// <param name="Key">Setting name (echo from the request).</param>
/// <param name="Outcome">Machine-readable outcome.</param>
/// <param name="ErrorCode">
/// Machine-readable error code for non-success outcomes. Maps to a localization key the client
/// resolves to a user-facing message. <see langword="null"/> when <see cref="Outcome"/> is
/// <see cref="BulkSettingOutcome.Updated"/>.
/// </param>
public sealed record BulkSettingResult(string Key, BulkSettingOutcome Outcome, string? ErrorCode);

/// <summary>
/// Outcome of a single bulk setting update.
/// </summary>
public enum BulkSettingOutcome
{
    /// <summary>Value was applied at the target scope.</summary>
    Updated,

    /// <summary>No <see cref="Granit.Settings.Definitions.SettingDefinition"/> is registered for the given key.</summary>
    NotFound,

    /// <summary>The target provider (Global / Tenant) is not in the definition's allow-list.</summary>
    ProviderNotAllowed,

    /// <summary>The value is not parseable as the definition's <c>ValueKind</c> or not in its <c>AllowedValues</c>.</summary>
    ValidationFailed,
}
