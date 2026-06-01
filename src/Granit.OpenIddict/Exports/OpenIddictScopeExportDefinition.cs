using System.Text.Json;
using Granit.DataExchange.Export;
using Granit.OpenIddict.Entities.OpenIddict;

namespace Granit.OpenIddict.Exports;

public sealed class OpenIddictScopeExportDefinition : ExportDefinition<GranitOpenIddictScope>
{
    public override string Name => "Granit.OpenIddict.ScopeExport";

    protected override void Configure(ExportDefinitionBuilder<GranitOpenIddictScope> builder)
    {
        builder
            .IncludeId()
            .Field(s => s.Name)
            .Field(s => s.DisplayName)
            .Field(s => s.Description)
            .Field(s => s.TenantId)
            .Field(s => s.Properties)
            .ComplexField("Resources", s => ParseJsonArray(s.Resources));
    }

    private static string[] ParseJsonArray(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];
}
