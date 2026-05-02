namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape request for <c>PATCH /folders/{id}</c>.
/// </summary>
/// <param name="Name">New folder name (validated like <see cref="CreateFolderRequest.Name"/>).</param>
public sealed record RenameFolderRequest(string Name);
