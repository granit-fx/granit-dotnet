namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape request for <c>PATCH /documents/{id}</c>. Accepts a partial update —
/// fields left <c>null</c> are unchanged.
/// </summary>
/// <param name="Name">New name; <c>null</c> leaves the current name unchanged.</param>
/// <param name="Description">
/// New description. Use <c>null</c> to leave the description unchanged. To clear an
/// existing description, send an empty string — the server distinguishes "absent" from
/// "empty" via the <see cref="ClearDescription"/> flag.
/// </param>
/// <param name="ClearDescription">
/// When <c>true</c>, sets the description to <c>null</c> regardless of the
/// <see cref="Description"/> value. Use this flag rather than sending an empty string,
/// which would set a non-null empty description.
/// </param>
public sealed record RenameDocumentRequest(
    string? Name,
    string? Description,
    bool ClearDescription = false);
