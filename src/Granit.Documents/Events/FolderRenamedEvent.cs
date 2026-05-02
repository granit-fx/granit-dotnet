using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Folder</c> is renamed. A <see cref="FolderPathChangedEvent"/>
/// is emitted alongside since the materialised <c>Path</c> changes too.
/// </summary>
public sealed record FolderRenamedEvent(
    Guid FolderId,
    string OldName,
    string NewName) : IDomainEvent;
