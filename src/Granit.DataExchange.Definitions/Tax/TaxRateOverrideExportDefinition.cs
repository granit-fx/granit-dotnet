using Granit.DataExchange.Export;
using Granit.Tax.Domain;

namespace Granit.DataExchange.Definitions.Tax;

public sealed class TaxRateOverrideExportDefinition : ExportDefinition<TaxRateOverride>
{
    public override string Name => "Granit.Tax.TaxRateOverrideExport";

    protected override void Configure(ExportDefinitionBuilder<TaxRateOverride> builder)
    {
        builder
            .IncludeId()
            .Field(t => t.CountryCode)
            .Field(t => t.StandardRate, f => f.Format("#,##0.00"))
            .Field(t => t.ReducedRate, f => f.Format("#,##0.00"))
            .Field(t => t.EffectiveFrom, f => f.Format("O"))
            .Field(t => t.EffectiveTo, f => f.Format("O"))
            .Field(t => t.TenantId);
    }
}
