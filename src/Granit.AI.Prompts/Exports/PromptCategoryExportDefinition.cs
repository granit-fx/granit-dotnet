using Granit.AI.Prompts.Domain;
using Granit.DataExchange.Export;

namespace Granit.AI.Prompts.Exports;

/// <summary>
/// Export definition for prompt categories (ADR-020 pairing with
/// <see cref="Queries.PromptCategoryQueryDefinition"/>). Emits the category fields for admin take-out.
/// </summary>
public sealed class PromptCategoryExportDefinition : ExportDefinition<PromptCategory>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.Prompts.CategoriesExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<PromptCategory> builder)
    {
        builder
            .IncludeId()
            .Field(c => c.Name)
            .Field(c => c.IsSystem)
            .Field(c => c.TenantId)
            .IncludeAuditFields();
    }
}
