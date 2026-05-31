using Granit.DataExchange.Export;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Exports;

public sealed class TimelineEntryExportDefinition : ExportDefinition<TimelineEntry>
{
    public override string Name => "Granit.Timeline.TimelineEntryExport";

    protected override void Configure(ExportDefinitionBuilder<TimelineEntry> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.EntityType)
            .Field(e => e.EntityId)
            .Field(e => e.EntryType)
            .Field(e => e.Body)
            .Field(e => e.AuthorId)
            .Field(e => e.AuthorName)
            .Field(e => e.SourceKey)
            .Field(e => e.SourceId)
            .Field(e => e.ParentEntryId)
            .Field(e => e.IsDeleted)
            .Field(e => e.DeletedAt, f => f.Format("O"))
            .Field(e => e.DeletedBy)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy);
    }
}
