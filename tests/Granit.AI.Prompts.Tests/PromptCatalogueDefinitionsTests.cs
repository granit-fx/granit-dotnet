using Granit.AI.Prompts.Exports;
using Granit.AI.Prompts.Queries;
using Shouldly;
using Xunit;

namespace Granit.AI.Prompts.Tests;

public sealed class PromptCatalogueDefinitionsTests
{
    [Fact]
    public void Query_definition_exposes_the_catalogue_columns()
    {
        PromptTemplateQueryDefinition definition = new();

        definition.Name.ShouldBe("Granit.AI.Prompts.TemplatesQuery");
        definition.LocalizationResourceType.ShouldBe(typeof(AIPromptsLocalizationResource));
        definition.GetColumns().ShouldContain(c => c.PropertyName.Equals("name", StringComparison.OrdinalIgnoreCase));
        definition.GetColumns().ShouldContain(c => c.PropertyName.Equals("isSystem", StringComparison.OrdinalIgnoreCase));
        definition.GetGlobalSearchProperties().ShouldContain(p => p.Equals("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Export_definition_includes_the_id_and_the_content_field()
    {
        PromptTemplateExportDefinition definition = new();

        definition.Name.ShouldBe("Granit.AI.Prompts.TemplatesExport");
        definition.GetIncludeId().ShouldBeTrue();
        definition.GetFields().ShouldContain(f => f.PropertyPath.Equals("content", StringComparison.OrdinalIgnoreCase));
    }
}
