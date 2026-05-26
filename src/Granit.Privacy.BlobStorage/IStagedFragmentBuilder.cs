using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Privacy.BlobStorage;

/// <summary>
/// Helper that takes raw bytes produced by an <see cref="IPrivacyDataProvider"/>, uploads
/// them to the staging container, signs the integrity tag, and returns a
/// <see cref="StagedExportFragment"/> ready to be yielded by the provider's
/// <c>ExportAsync</c> enumerable.
/// </summary>
/// <remarks>
/// Lightweight providers (Identity, Auditing, Notifications) use this to factor out the
/// staging upload boilerplate. The builder is also where the HMAC integrity tag is computed
/// (via <c>Granit.Privacy.DataExport.Security.IExportHmacSigner</c>) — providers never deal
/// with HMAC details directly. Paths are sanitised via
/// <c>Granit.Privacy.DataExport.Sanitization.EntryPathSanitizer</c>.
/// </remarks>
public interface IStagedFragmentBuilder
{
    /// <summary>
    /// Serialises <paramref name="dto"/> as JSON UTF-8, uploads to the staging container,
    /// signs the integrity tag and returns the resulting fragment.
    /// </summary>
    /// <param name="context">Export context — request id, subject, tenant, regulation.</param>
    /// <param name="providerName">Originating <see cref="IPrivacyDataProvider.ProviderName"/>;
    /// part of the HMAC binding.</param>
    /// <param name="entryPath">Path of the fragment inside the final archive (sanitised by
    /// the builder before upload).</param>
    /// <param name="dto">DTO to serialise.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<StagedExportFragment> BuildJsonAsync<T>(
        PrivacyExportContext context,
        string providerName,
        string entryPath,
        T dto,
        CancellationToken cancellationToken)
        where T : notnull;
}
