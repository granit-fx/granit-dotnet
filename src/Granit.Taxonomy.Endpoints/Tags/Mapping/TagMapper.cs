using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Tags.Dtos;

namespace Granit.Taxonomy.Endpoints.Tags.Mapping;

internal static class TagMapper
{
    public static TagResponse ToResponse(this Tag tag) =>
        new(tag.Id, tag.TenantId, tag.Scope, tag.Name, tag.Color, tag.HideOnEntityCard, tag.RowVersion);
}
