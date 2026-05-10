namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>Wire-shape row in the F8.2 trash listing.</summary>
/// <param name="Id">Document identifier.</param>
/// <param name="FolderId">Folder the document was in when it was trashed.</param>
/// <param name="Name">User-facing display name.</param>
/// <param name="OwnerUserId">Document owner.</param>
/// <param name="TrashedAt">UTC instant the document was sent to the trash.</param>
/// <param name="DaysUntilPermanentDeletion">
/// Number of whole days remaining before the empty-trash background job (F9.2)
/// permanently deletes the row, derived from <see cref="TrashedAt"/> +
/// <c>GranitDocumentsOptions.TrashRetentionDays</c>. Negative values are clamped at zero
/// — past-due rows are awaiting the next cleanup run.
/// </param>
public sealed record TrashedDocumentResponse(
    Guid Id,
    Guid FolderId,
    string Name,
    Guid OwnerUserId,
    DateTimeOffset TrashedAt,
    int DaysUntilPermanentDeletion);

/// <summary>Wire-shape response for <c>GET /documents/trash</c>.</summary>
public sealed record ListTrashedDocumentsResponse(
    IReadOnlyList<TrashedDocumentResponse> Documents,
    long TotalCount,
    int Skip,
    int Take);
