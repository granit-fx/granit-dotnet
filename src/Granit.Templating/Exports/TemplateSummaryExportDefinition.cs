using Granit.DataExchange.Export;
using Granit.Templating.Store;

namespace Granit.Templating.Exports;

/// <summary>
/// Export definition for template summaries — paired with
/// <c>Granit.Templating.TemplateSummaryQuery</c>.
/// </summary>
public sealed class TemplateSummaryExportDefinition : ExportDefinition<TemplateSummary>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Templating.TemplateSummaryExport";

    /// <inheritdoc/>
    public override string? QueryDefinitionName => "Granit.Templating.TemplateSummaryQuery";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<TemplateSummary> builder)
    {
        builder
            .Field(s => s.TenantId)
            .Field(s => s.Name)
            .Field(s => s.Culture)
            .Field(s => s.MimeType)
            .Field(s => s.CurrentStatus)
            .Field(s => s.HasPublishedVersion)
            .Field(s => s.LayoutName)
            .Field(s => s.CategoryId)
            .Field(s => s.LastModifiedAt, f => f.Format("O"))
            .Field(s => s.LastModifiedBy);
    }
}
