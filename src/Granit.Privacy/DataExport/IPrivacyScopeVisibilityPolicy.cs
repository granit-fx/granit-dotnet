namespace Granit.Privacy.DataExport;

/// <summary>
/// Host extension point that gates which provider scopes the data subject can see in
/// the export selector (<c>GET /privacy/exports/scopes</c>) and request.
/// </summary>
/// <remarks>
/// <para>
/// The framework runs three gates before consulting this policy:
/// <list type="number">
///   <item>Module loaded — only providers registered with
///   <c>AddDataProvider&lt;TProvider&gt;()</c> participate.</item>
///   <item><see cref="IPrivacyDataProvider.HasDataAsync"/> — providers with no data for
///   the subject are hidden (UX: Google Takeout-style "if you never used Photos, Photos
///   doesn't appear").</item>
///   <item>This policy — host decides whether to additionally filter by feature flags,
///   per-tenant configuration, custom permissions, etc.</item>
/// </list>
/// </para>
/// <para>
/// <b>GDPR Art. 15/20 reminder:</b> Article 20 portability is a personal right, not a
/// usage right. A "reader" of Documents must still be able to export her own documents
/// even without an admin permission. Hosts overriding this policy should think hard
/// before denying a scope to a subject who has data in it — denying GDPR Art. 15 access
/// generally requires a documented legal basis.
/// </para>
/// <para>
/// The default implementation (<see cref="AllowAllPrivacyScopeVisibilityPolicy"/>) is
/// fail-open. Hosts wire a custom policy via DI to layer feature-flag or fine-grained
/// permission checks on top.
/// </para>
/// </remarks>
public interface IPrivacyScopeVisibilityPolicy
{
    /// <summary>
    /// Returns <see langword="true"/> when the scope should be visible to the subject for
    /// this request.
    /// </summary>
    ValueTask<bool> IsVisibleAsync(
        ProviderDescriptor descriptor,
        PrivacyExportContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default fail-open <see cref="IPrivacyScopeVisibilityPolicy"/> — every provider that
/// passes the module-loaded and HasData gates stays visible. Production hosts that need
/// feature-flag or permission-based filtering replace this via DI.
/// </summary>
internal sealed class AllowAllPrivacyScopeVisibilityPolicy : IPrivacyScopeVisibilityPolicy
{
    public ValueTask<bool> IsVisibleAsync(
        ProviderDescriptor descriptor,
        PrivacyExportContext context,
        CancellationToken cancellationToken) => ValueTask.FromResult(true);
}
