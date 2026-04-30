namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Wire-shape projection of <see cref="EntityViewSharedWith"/>.</summary>
/// <param name="Roles">Role names granted access to the view.</param>
/// <param name="Users">User identifiers granted access to the view.</param>
public sealed record EntityViewSharedWithDto(
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> Users);
