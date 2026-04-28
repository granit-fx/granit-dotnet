using Granit.DataExchange.Export;
using Granit.Parties.Domain;

namespace Granit.Parties.Exports;

/// <summary>CSV / XLSX export whitelist for parties.</summary>
public sealed class PartyExportDefinition : ExportDefinition<Party>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Parties.PartyExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Party> builder)
    {
        builder
            .IncludeId()
            .Field(c => c.Kind)
            .Field(c => c.Name)
            .Field(c => c.DefaultCurrency)
            .Field(c => c.Timezone)
            .Field(c => c.Language)
            .Field(c => c.Website)
            .Field(c => c.TaxId)
            .Field(c => c.RegistrationNumber)
            .Field(c => c.Roles)
            .Field(c => c.Status)
            .Field(c => c.CreatedAt, f => f.Format("O"))
            .Field(c => c.CreatedBy)
            .Field(c => c.ModifiedAt, f => f.Format("O"))
            .Field(c => c.ModifiedBy);
    }
}
