namespace Granit.Documents;

/// <summary>
/// Public constants for the <c>Granit.BlobStorage</c> containers owned by
/// <c>Granit.Documents</c>. Composing modules (e.g. the rendition background jobs
/// reading source bytes by presigned URL) reference these instead of duplicating
/// the literal string.
/// </summary>
public static class DocumentBlobContainers
{
    /// <summary>Container holding the original document blob for every <c>DocumentVersion</c>.</summary>
    public const string Documents = "documents";
}
