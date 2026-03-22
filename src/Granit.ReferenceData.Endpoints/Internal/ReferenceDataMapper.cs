using Granit.Core.Domain;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;

namespace Granit.ReferenceData.Endpoints.Internal;

/// <summary>
/// Maps <see cref="ReferenceDataEntity"/> instances to <see cref="ReferenceDataResponse"/> DTOs,
/// ensuring EF entities (and their <c>ExtraPropertiesJson</c> column) are never exposed directly.
/// </summary>
internal static class ReferenceDataMapper
{
    internal static ReferenceDataResponse ToResponse<TEntity>(TEntity entity)
        where TEntity : ReferenceDataEntity
    {
        IReadOnlyDictionary<string, string> extras = entity.GetExtraProperties();

        return new ReferenceDataResponse(
            entity.Id,
            entity.Code,
            entity.Label,
            entity.LabelEn,
            entity.LabelFr,
            entity.LabelNl,
            entity.LabelDe,
            entity.LabelEs,
            entity.LabelIt,
            entity.LabelPt,
            entity.LabelZh,
            entity.LabelJa,
            entity.LabelPl,
            entity.LabelTr,
            entity.LabelKo,
            entity.LabelSv,
            entity.LabelCs,
            entity.IsActive,
            entity.SortOrder,
            entity.ValidFrom,
            entity.ValidTo,
            entity.ParentCode,
            extras.Count > 0 ? new Dictionary<string, string>(extras) : null);
    }
}
