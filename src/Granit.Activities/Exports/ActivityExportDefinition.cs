using Granit.Activities.Domain;
using Granit.DataExchange.Export;

namespace Granit.Activities.Exports;

/// <summary>
/// Export definition for activities — whitelists the columns produced by
/// CSV / XLSX exports of the activities admin grid.
/// </summary>
public sealed class ActivityExportDefinition : ExportDefinition<Activity>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Activities.ActivityExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Activity> builder)
    {
        builder
            .IncludeId()
            .Field(a => a.TenantId)
            .Field(a => a.EntityType)
            .Field(a => a.EntityId)
            .Field(a => a.Type)
            .Field(a => a.Status)
            .Field(a => a.AssignedToUserId)
            .Field(a => a.CreatedByUserId)
            .Field(a => a.CompletedByUserId)
            .Field(a => a.DueAt, f => f.Format("O"))
            .Field(a => a.CompletedAt, f => f.Format("O"))
            .Field(a => a.OverdueNotifiedAt, f => f.Format("O"))
            .Field(a => a.Description)
            .Field(a => a.CreatedAt, f => f.Format("O"))
            .Field(a => a.CreatedBy)
            .Field(a => a.ModifiedAt, f => f.Format("O"))
            .Field(a => a.ModifiedBy);
    }
}
