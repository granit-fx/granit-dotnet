using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.QueryEngine;

namespace Granit.Parties.Deduplication.Queries;

/// <summary>
/// QueryEngine definition for the duplicate-candidates admin grid — declares the columns,
/// filters, sorts, and pagination metadata that drive
/// <c>GET /parties/duplicates</c> and <c>GET /parties/duplicates/meta</c>.
/// </summary>
/// <remarks>
/// <para>
/// The signals JSON blob is intentionally NOT exposed as a column — it is consumed by the
/// per-row detail view (<c>GET /parties/duplicates/{id}</c> in a future iteration), not by
/// the grid. <c>TenantId</c> is also omitted — every query is auto-scoped to the current
/// tenant by the <c>IMultiTenant</c> query filter, so surfacing it as a filterable column
/// would be both pointless and a footgun.
/// </para>
/// </remarks>
public sealed class DuplicateCandidateQueryDefinition : QueryDefinition<PartyDuplicateCandidate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Parties.Deduplication.DuplicateCandidateQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PartyDuplicateCandidate> builder)
    {
        builder
            .Column(c => c.Score, col => col
                .Label("Score").LabelKey("Parties.Duplicates.Columns.Score")
                .Filterable().Sortable())
            .Column(c => c.Tier, col => col
                .Label("Tier").LabelKey("Parties.Duplicates.Columns.Tier")
                .Filterable().Sortable())
            .Column(c => c.PartyId, col => col
                .Label("Party").LabelKey("Parties.Duplicates.Columns.PartyId")
                .Filterable())
            .Column(c => c.CandidateId, col => col
                .Label("Candidate").LabelKey("Parties.Duplicates.Columns.CandidateId")
                .Filterable())
            .Column(c => c.DismissedAt, col => col
                .Label("Dismissed").LabelKey("Parties.Duplicates.Columns.DismissedAt")
                .Filterable().Sortable())
            .Column(c => c.CreatedAt, col => col
                .Label("Detected").LabelKey("Parties.Duplicates.Columns.CreatedAt")
                .Filterable().Sortable())
            .Column(c => c.UpdatedAt, col => col
                .Label("Refreshed").LabelKey("Parties.Duplicates.Columns.UpdatedAt")
                .Sortable())
            .DateFilter(c => c.CreatedAt)
            .DefaultSort("-score")
            .DefaultPageSize(50);
    }
}
