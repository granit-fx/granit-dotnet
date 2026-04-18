using Granit.Auditing.Domain;
using Granit.DataExchange.Export;

namespace Granit.Auditing.Exports;

public sealed class AuditEntityChangeExportDefinition : ExportDefinition<AuditEntityChange>
{
    public override string Name => "Granit.Auditing.AuditEntityChangeExport";

    protected override void Configure(ExportDefinitionBuilder<AuditEntityChange> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.AuditEntryId)
            .Field(e => e.EntityType)
            .Field(e => e.EntityId)
            .Field(e => e.ChangeType);
    }
}
