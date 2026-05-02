using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Well-known identifiers shared between the privacy export handler base
/// (<c>Granit.Privacy.BlobStorage</c>) and the archive assembler (<c>Granit.Privacy.BackgroundJobs</c>).
/// </summary>
public static class PrivacyExportContainerNames
{
    /// <summary>Blob-storage container where fragments and the final ZIP archive are stored.</summary>
    public const string FragmentContainer = "gdpr-exports";

    /// <summary>
    /// Prefix used in <see cref="Events.PersonalDataPreparedEto.BlobReferenceId"/> when a
    /// provider has no data for the user. The assembler skips these fragments and records
    /// the provider in the manifest's <c>EmptyProviders</c> list rather than calling
    /// <c>CreateDownloadUrlAsync</c> with a non-blob sentinel.
    /// </summary>
    public const string EmptyFragmentPrefix = "empty:";

    /// <summary>
    /// Blob key convention for the final assembled ZIP archive.
    /// Matches <c>ExportCompletedEto.ArchiveBlobReferenceId</c> used by the saga.
    /// </summary>
    public static BlobReference ArchiveBlobReferenceId(Guid requestId) =>
        BlobReference.Create($"personal-data-export/{requestId}");
}
