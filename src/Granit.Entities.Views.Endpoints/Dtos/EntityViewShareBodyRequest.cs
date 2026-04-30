namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Request body for <c>POST /entities/{name}/views/{id}/share</c>.</summary>
/// <param name="Roles">Role names granted access to the view.</param>
/// <param name="Users">User identifiers granted access to the view.</param>
public sealed record EntityViewShareBodyRequest(
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> Users);
