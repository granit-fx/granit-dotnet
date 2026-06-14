using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Queries;
using Granit.DataExchange.Export;

namespace Granit.Authentication.ApiKeys.Exports;

/// <summary>
/// Export definition for API keys. Mirrors the list-view columns and reuses
/// <see cref="ApiKeyEntryQueryDefinition"/> so exports honour the same filtering and sorting
/// pipeline as the grid. The raw secret (<c>HashedKey</c>) is never exported.
/// </summary>
public sealed class ApiKeyEntryExportDefinition : ExportDefinition<ApiKeyEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Authentication.ApiKeys.ApiKeyEntryExport";

    /// <inheritdoc/>
    public override string? QueryDefinitionName => "Granit.Authentication.ApiKeys.ApiKeyEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<ApiKeyEntry> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.Name)
            .Field(e => e.Type)
            .Field(e => e.Environment)
            .Field(e => e.Prefix)
            .Field(e => e.LastFourChars)
            .Field(e => e.ExpiresAt, f => f.Format("O"))
            .Field(e => e.LastUsedAt, f => f.Format("O"))
            .Field(e => e.RevokedAt, f => f.Format("O"))
            .Field(e => e.CacheBehavior)
            .Field(e => e.TenantId)
            .IncludeCreationAuditFields();
    }
}
