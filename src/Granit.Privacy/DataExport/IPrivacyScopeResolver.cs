namespace Granit.Privacy.DataExport;

/// <summary>
/// Resolves which provider scopes are visible to a data subject for a given export
/// request, by composing three gates over the
/// <see cref="IDataProviderRegistry"/>:
/// <list type="number">
///   <item><b>Module loaded</b> — only providers registered via
///   <c>AddDataProvider&lt;TProvider&gt;()</c>.</item>
///   <item><b>HasData probe</b> — <see cref="IPrivacyDataProvider.HasDataAsync"/>;
///   providers with no data for this subject are hidden (Takeout-style UX).</item>
///   <item><b>Visibility policy</b> — host-supplied
///   <see cref="IPrivacyScopeVisibilityPolicy"/> for feature-flag /
///   per-tenant filtering.</item>
/// </list>
/// </summary>
/// <remarks>
/// The saga calls this resolver when the export starts so it knows which providers to
/// expect fragments from (<c>ExpectedCount</c> + <c>PendingProviders</c>). The scopes
/// endpoint calls it to populate the UI selector.
/// </remarks>
public interface IPrivacyScopeResolver
{
    /// <summary>
    /// Returns the <see cref="ProviderDescriptor"/>s visible to the subject for this
    /// request, after applying all gates in registration order.
    /// </summary>
    Task<IReadOnlyList<ProviderDescriptor>> ListVisibleAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken);
}
