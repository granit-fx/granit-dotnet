using Granit.AI.Prompts.Domain;
using Granit.DataExchange.Export;

namespace Granit.AI.Prompts.Exports;

/// <summary>
/// Export definition for the prompt catalogue (ADR-020 pairing with
/// <see cref="Queries.PromptTemplateQueryDefinition"/>). Emits the catalogue fields, including the
/// instruction text, for admin take-out.
/// </summary>
public sealed class PromptTemplateExportDefinition : ExportDefinition<PromptTemplate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.Prompts.TemplatesExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<PromptTemplate> builder)
    {
        builder
            .IncludeId()
            .Field(p => p.Name)
            .Field(p => p.ShortDescription)
            .Field(p => p.Content)
            .Field(p => p.Icon)
            .Field(p => p.IsSystem)
            .Field(p => p.Version)
            .Field(p => p.OwnerId)
            .Field(p => p.TenantId)
            .IncludeAuditFields();
    }
}
