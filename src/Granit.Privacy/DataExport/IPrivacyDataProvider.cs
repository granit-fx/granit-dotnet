namespace Granit.Privacy.DataExport;

/// <summary>
/// Declarative contract for a module that contributes personal data to the privacy export
/// scatter-gather saga (GDPR Art. 15 / 20, LGPD Art. 18, CCPA).
/// </summary>
/// <remarks>
/// <para>
/// Implementations are discovered via DI (registered with <c>AddDataProvider&lt;TProvider&gt;()</c>)
/// and invoked by a <c>PrivacyDataProviderHandlerBase&lt;TProvider&gt;</c> Wolverine handler
/// when <see cref="Events.PersonalDataRequestedEto"/> is published.
/// </para>
/// <para>
/// Providers expose only what they know: the bytes of the user's data and how to name / type
/// the fragment. Upload to blob storage and event publication are handled by the base class.
/// </para>
/// </remarks>
public interface IPrivacyDataProvider
{
    /// <summary>
    /// Globally unique provider identifier registered via <c>RegisterDataProvider(name)</c>.
    /// Used by <c>PersonalDataExportSaga</c> to track which providers have responded.
    /// </summary>
    static abstract string ProviderName { get; }

    /// <summary>MIME content type of the fragment produced by <see cref="ExportAsync"/>.</summary>
    static abstract string ContentType { get; }

    /// <summary>
    /// File name under which the fragment is stored in the final archive.
    /// Typically <c>"{provider-name}-{requestId}.{ext}"</c>.
    /// </summary>
    static abstract string FileName(Guid requestId);

    /// <summary>
    /// Produces the personal-data fragment for the given user, or an empty buffer when the
    /// provider has no data (the handler base emits an <c>empty:</c> sentinel in that case).
    /// </summary>
    /// <remarks>
    /// The returned bytes are uploaded in full to blob storage before the saga publishes
    /// <see cref="Events.PersonalDataPreparedEto"/>. Large fragments should still fit within
    /// the per-provider budget derived from <c>GranitPrivacyOptions.ExportMaxSizeMb</c>.
    /// Streaming exports (paged JSON) should buffer to a <see cref="System.IO.MemoryStream"/>
    /// or temp file and materialise once.
    /// </remarks>
    Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken);
}
