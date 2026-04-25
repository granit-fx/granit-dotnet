using Granit.DataExchange.Export;
using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Exports;

public sealed class DynamicReferenceDataEntityExportDefinition : ExportDefinition<DynamicReferenceDataEntity>
{
    public override string Name => "Granit.ReferenceData.DynamicReferenceDataEntityExport";

    protected override void Configure(ExportDefinitionBuilder<DynamicReferenceDataEntity> builder)
    {
        builder
            .IncludeId()
            .IncludeMetadata()
            .Field(e => e.Code)
            .Field(e => e.LabelEn)
            .Field(e => e.LabelFr)
            .Field(e => e.LabelNl)
            .Field(e => e.LabelDe)
            .Field(e => e.LabelEs)
            .Field(e => e.LabelIt)
            .Field(e => e.LabelPt)
            .Field(e => e.LabelZh)
            .Field(e => e.LabelJa)
            .Field(e => e.LabelPl)
            .Field(e => e.LabelTr)
            .Field(e => e.LabelKo)
            .Field(e => e.LabelSv)
            .Field(e => e.LabelCs)
            .Field(e => e.LabelHi)
            .Field(e => e.Activated)
            .Field(e => e.SortOrder)
            .Field(e => e.ValidFrom, f => f.Format("O"))
            .Field(e => e.ValidTo, f => f.Format("O"))
            .Field(e => e.ParentCode)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
