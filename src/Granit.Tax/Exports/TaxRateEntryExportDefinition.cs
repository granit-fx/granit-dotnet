using Granit.DataExchange.Export;

namespace Granit.Tax.Exports;

/// <summary>
/// Export definition for effective tax rate entries resolved by
/// <see cref="ITaxRateProvider"/>.
/// </summary>
public sealed class TaxRateEntryExportDefinition : ExportDefinition<TaxRateEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Tax.TaxRateEntryExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<TaxRateEntry> builder)
    {
        builder
            .Field(r => r.CountryCode)
            .Field(r => r.StandardRate, f => f.Format("#,##0.00"))
            .Field(r => r.ReducedRate, f => f.Format("#,##0.00"))
            .Field(r => r.SuperReducedRate, f => f.Format("#,##0.00"))
            .Field(r => r.ParkingRate, f => f.Format("#,##0.00"))
            .Field(r => r.EffectiveFrom, f => f.Format("O"))
            .Field(r => r.EffectiveTo, f => f.Format("O"));
    }
}
