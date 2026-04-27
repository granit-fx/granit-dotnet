using Granit.DataExchange.Export;
using Granit.Parties.EntityFrameworkCore.Deduplication;

namespace Granit.Parties.Deduplication.Exports;

/// <summary>
/// CSV / XLSX export whitelist for the duplicate-candidates review table. Mirrors the
/// columns surfaced by <c>DuplicateCandidateQueryDefinition</c> with one addition — the
/// raw signals JSON blob ships in the export so a finance / privacy reviewer can audit
/// why each pair was flagged without round-tripping through the per-row detail view.
/// </summary>
public sealed class DuplicateCandidateExportDefinition : ExportDefinition<PartyDuplicateCandidate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Parties.Deduplication.DuplicateCandidateExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<PartyDuplicateCandidate> builder)
    {
        builder
            .IncludeId()
            .Field(c => c.PartyId)
            .Field(c => c.CandidateId)
            .Field(c => c.Tier)
            .Field(c => c.Score)
            .Field(c => c.SignalsJson)
            .Field(c => c.DismissedAt, f => f.Format("O"))
            .Field(c => c.CreatedAt, f => f.Format("O"))
            .Field(c => c.UpdatedAt, f => f.Format("O"));
    }
}
