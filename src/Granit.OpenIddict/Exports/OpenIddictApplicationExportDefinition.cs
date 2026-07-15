using System.Text.Json;
using Granit.DataExchange.Export;
using Granit.OpenIddict.Models;

namespace Granit.OpenIddict.Exports;

public sealed class OpenIddictApplicationExportDefinition : ExportDefinition<OpenIddictApplicationModel>
{
    public override string Name => "Granit.OpenIddict.ApplicationExport";

    protected override void Configure(ExportDefinitionBuilder<OpenIddictApplicationModel> builder)
    {
        builder
            .IncludeId()
            .Field(a => a.ClientId)
            .Field(a => a.DisplayName)
            .Field(a => a.ClientType)
            .Field(a => a.ConsentType)
            .Field(a => a.ApplicationType)
            .Field(a => a.TenantId)
            // JsonWebKeySet is a public key — safe to export for private_key_jwt round-trip.
            // Stored as a raw JSON string by OpenIddict; preserved as-is for migration.
            .Field(a => a.JsonWebKeySet)
            // Properties stores the extension bag (e.g. urn:granit:openiddict:client_side).
            // Raw JSON string — round-trip import re-applies it via descriptor seeding.
            .Field(a => a.Properties)
            // Array fields are stored as JSON strings by OpenIddict; deserialized here so
            // JSON/XML writers emit them as proper arrays rather than escaped strings.
            .ComplexField("Permissions", a => ParseJsonArray(a.Permissions))
            .ComplexField("RedirectUris", a => ParseJsonArray(a.RedirectUris))
            .ComplexField("PostLogoutRedirectUris", a => ParseJsonArray(a.PostLogoutRedirectUris))
            .ComplexField("Requirements", a => ParseJsonArray(a.Requirements));
        // Intentionally excluded: ClientSecret — encrypted at rest, not portable across
        // instances with different data-protection keys. Rotate on the target after migration.
    }

    private static string[] ParseJsonArray(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];
}
