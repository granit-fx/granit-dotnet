using Granit.Entities.Views.Domain;

namespace Granit.Entities.Views.EntityFrameworkCore.Internal;

/// <summary>
/// Aggregate → wire-shape projection used by the read service.
/// </summary>
internal static class EntityViewMapping
{
    public static EntityViewDescriptor ToDescriptor(this EntityView view) =>
        new(
            Id: view.Id,
            EntityName: view.EntityName,
            BasedOn: view.BasedOn,
            Kind: view.Kind,
            Name: view.Name,
            Description: view.Description,
            Icon: view.Icon,
            State: view.State,
            Visibility: view.Visibility,
            OwnerId: view.OwnerId,
            SharedWith: view.SharedWith,
            IsPinned: view.IsPinned,
            IsDefault: view.IsDefault,
            IsPersonalDefault: view.IsPersonalDefault,
            SortOrder: view.SortOrder);
}
