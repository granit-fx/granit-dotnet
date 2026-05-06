using System.Text.Json.Nodes;

namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Wire-shape projection of an <c>EntityView</c> aggregate.</summary>
public sealed record EntityViewResponse(
    Guid Id,
    string EntityName,
    string BasedOn,
    string Kind,
    string Name,
    string? Description,
    string? Icon,
    JsonObject State,
    EntityViewVisibility Visibility,
    Guid? OwnerId,
    EntityViewSharedWithResponse? SharedWith,
    bool IsPinned,
    bool IsDefault,
    bool IsPersonalDefault,
    int SortOrder)
{
    /// <summary>Project a domain descriptor into the wire shape.</summary>
    public static EntityViewResponse FromDescriptor(EntityViewDescriptor descriptor) =>
        new(
            descriptor.Id,
            descriptor.EntityName,
            descriptor.BasedOn,
            descriptor.Kind,
            descriptor.Name,
            descriptor.Description,
            descriptor.Icon,
            descriptor.State,
            descriptor.Visibility,
            descriptor.OwnerId,
            descriptor.SharedWith is null
                ? null
                : new EntityViewSharedWithResponse(descriptor.SharedWith.Roles, descriptor.SharedWith.Users),
            descriptor.IsPinned,
            descriptor.IsDefault,
            descriptor.IsPersonalDefault,
            descriptor.SortOrder);
}
