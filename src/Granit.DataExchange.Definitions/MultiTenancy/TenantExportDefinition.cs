using Granit.DataExchange.Export;
using Granit.MultiTenancy.EntityFrameworkCore.Entities;

namespace Granit.DataExchange.Definitions.MultiTenancy;

public sealed class TenantExportDefinition : ExportDefinition<Tenant>
{
    public override string Name => "Granit.MultiTenancy.TenantExport";

    protected override void Configure(ExportDefinitionBuilder<Tenant> builder)
    {
        builder
            .IncludeId()
            .Field(t => t.Name)
            .Field(t => t.Identifier)
            .Field(t => t.ContactEmail)
            .Field(t => t.Jurisdiction)
            .Field(t => t.Activated)
            .Field(t => t.CustomDomain)
            .Field(t => t.IsDeleted)
            .Field(t => t.DeletedAt, f => f.Format("O"))
            .Field(t => t.DeletedBy)
            .Field(t => t.CreatedAt, f => f.Format("O"))
            .Field(t => t.CreatedBy)
            .Field(t => t.ModifiedAt, f => f.Format("O"))
            .Field(t => t.ModifiedBy);
    }
}
