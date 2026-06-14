using Granit.DataExchange.Export;
using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Exports;

/// <summary>CSV/XLSX export definition for managed hostnames (paired with the query definition).</summary>
public sealed class ManagedHostnameExportDefinition : ExportDefinition<ManagedHostname>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Hostnames.ManagedHostnameExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<ManagedHostname> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.Host.Value)
            .Field(e => e.OwnerType)
            .Field(e => e.OwnerId)
            .Field(e => e.IsPrimary)
            .Field(e => e.Status)
            .IncludeAuditFields();
    }
}
