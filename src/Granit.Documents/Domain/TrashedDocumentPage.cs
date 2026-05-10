namespace Granit.Documents.Domain;

/// <summary>
/// Paged slice of trashed documents returned by
/// <see cref="IDocumentService.ListTrashedAsync"/> (F8.2). Carries a snapshot of every row
/// the endpoint exposes including <see cref="TrashedAt"/> so the response can compute the
/// retention countdown.
/// </summary>
public sealed record TrashedDocumentPage(
    IReadOnlyList<TrashedDocumentRow> Documents,
    long TotalCount);

/// <summary>
/// Domain-level row for a trashed document — the API DTO maps over this and adds the
/// computed <c>daysUntilPermanentDeletion</c>.
/// </summary>
public sealed record TrashedDocumentRow(
    Guid Id,
    Guid FolderId,
    string Name,
    Guid OwnerUserId,
    DateTimeOffset TrashedAt);
