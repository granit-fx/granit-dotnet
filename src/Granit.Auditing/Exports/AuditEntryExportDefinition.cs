using Granit.Auditing.Domain;
using Granit.DataExchange.Export;

namespace Granit.Auditing.Exports;

public sealed class AuditEntryExportDefinition : ExportDefinition<AuditEntry>
{
    public override string Name => "Granit.Auditing.AuditEntryExport";

    protected override void Configure(ExportDefinitionBuilder<AuditEntry> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.Timestamp, f => f.Format("O"))
            .Field(e => e.UserId)
            .Field(e => e.UserName)
            .Field(e => e.Category)
            .Field(e => e.IpAddress)
            .Field(e => e.UserAgent)
            .Field(e => e.CorrelationId)
            .Field(e => e.TenantId)
            .IncludeCreationAuditFields();
    }
}
