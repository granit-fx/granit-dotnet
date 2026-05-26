namespace Granit.Privacy.DataExport;

/// <summary>
/// Captured registration record for a single <see cref="IPrivacyDataProvider"/>.
/// Built by <c>GranitPrivacyBuilder.AddDataProvider&lt;TProvider&gt;()</c> at startup;
/// consumed by <see cref="IPrivacyScopeResolver"/> to evaluate the visibility gates
/// without taking a hard dep on the typed provider.
/// </summary>
/// <param name="ProviderName">Stable identifier (e.g. <c>"identity-local"</c>).</param>
/// <param name="DisplayKey">Localisation key for the scope selector UI.</param>
/// <param name="FeatureName">Optional feature flag controlling visibility — interpreted
/// by the host's <see cref="IPrivacyScopeVisibilityPolicy"/>.</param>
/// <param name="HasDataProbe">Resolves the typed provider from
/// <see cref="IServiceProvider"/> at scope-evaluation time and calls
/// <see cref="IPrivacyDataProvider.HasDataAsync"/>. Captured by the builder so callers
/// of <see cref="IPrivacyScopeResolver"/> don't need to know the concrete provider type.</param>
public sealed record ProviderRegistration(
    string ProviderName,
    string DisplayKey,
    string? FeatureName,
    Func<IServiceProvider, PrivacyExportContext, CancellationToken, ValueTask<bool>> HasDataProbe);
