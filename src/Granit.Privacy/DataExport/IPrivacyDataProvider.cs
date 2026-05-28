using Granit.Privacy.DataExport.Fragments;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Declarative contract for a module that contributes personal data to the privacy export
/// scatter-gather saga (GDPR Art. 15 / 20, LGPD Art. 18, CCPA).
/// </summary>
/// <remarks>
/// <para>
/// Implementations are discovered via DI (registered with <c>AddDataProvider&lt;TProvider&gt;()</c>)
/// and invoked by a per-provider Wolverine handler when
/// <see cref="Events.PersonalDataRequestedEto"/> is published. Each handler forwards to
/// <c>PrivacyFragmentUploader</c>, which iterates the fragments yielded by the provider and
/// emits one <see cref="Events.PersonalDataPreparedEto"/> per fragment.
/// </para>
/// <para>
/// <b>Migration note (Takeout-style, P6.1):</b> the previous shape
/// <c>Task&lt;ReadOnlyMemory&lt;byte&gt;&gt; ExportAsync(Guid userId, CT)</c> imposed a single
/// in-memory buffer per provider plus a global size cap incompatible with blob-backed
/// modules (Documents, attachments). Providers now stream one or more
/// <see cref="ExportFragment"/> values via <see cref="ExportAsync"/>.
/// </para>
/// <para>
/// <b>Tenant scope invariant.</b> Implementations that depend on a tenant-isolated
/// <c>DbContext</c> (registered via <c>AddGranitIsolatedDbContext</c>) require an active
/// <c>ICurrentTenant</c> at construction time, otherwise the DbContext silently falls
/// back to the <c>SharedDatabase</c> keyed factory and queries the wrong schema
/// (PostgreSQL 42P01). The framework already anchors the tenant for you on the two
/// official orchestration paths: <c>PersonalDataExportSaga.Start</c> (HasData probe) and
/// the per-provider Wolverine handlers (via <see cref="Events.PersonalDataRequestedEto.TenantId"/>
/// restored on the receiving side by <c>TenantContextBehavior</c>). Custom orchestrators
/// that resolve an <see cref="IPrivacyDataProvider"/> directly MUST do the same — open
/// <c>currentTenant.Change(context.TenantId)</c> before resolving the provider.
/// </para>
/// </remarks>
public interface IPrivacyDataProvider
{
    /// <summary>
    /// Globally unique provider identifier. Used by <c>PersonalDataExportSaga</c> to track
    /// which providers have responded and by the scope selector
    /// (<c>GET /privacy/exports/scopes</c>) to address this provider.
    /// </summary>
    static abstract string ProviderName { get; }

    /// <summary>
    /// Localisation key for the scope selector UI (e.g. <c>"Privacy.Scopes.IdentityLocal"</c>).
    /// Architecture test enforces that the key resolves in all 18 framework cultures.
    /// </summary>
    static abstract string DisplayKey { get; }

    /// <summary>
    /// Optional feature flag controlling visibility of this provider in the scope selector.
    /// When <see langword="null"/>, the provider is always visible. When non-null, the
    /// selector hides the scope if <c>IFeatureManager.IsEnabledAsync</c> returns false —
    /// mirroring the Notifications visibility pattern.
    /// </summary>
    static abstract string? FeatureName { get; }

    /// <summary>
    /// Fast probe used by the scope selector and the assembly visibility gate. Returns
    /// <see langword="true"/> when the subject has any data this provider could export.
    /// </summary>
    /// <remarks>
    /// MUST be cheap (count-style query with a tenant/owner filter, &lt; 50 ms target).
    /// NEVER perform a full scan — the selector calls this on every page load.
    /// </remarks>
    ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Produces the personal-data fragments for the request. Lightweight providers yield a
    /// single <see cref="StagedExportFragment"/>; blob-backed providers (Documents,
    /// attachments) yield one <see cref="PassThroughExportFragment"/> per source blob.
    /// </summary>
    /// <remarks>
    /// Fragments MUST be signed with
    /// <see cref="Security.IExportHmacSigner.Sign"/>; the archive assembler rejects unsigned
    /// or tampered fragments and routes them to the DLQ (closes VULN-001 / VULN-102).
    /// Yielding an empty enumerable is legal and translates to <c>EmptyProviders</c> in the
    /// manifest.
    /// </remarks>
    IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken);
}
