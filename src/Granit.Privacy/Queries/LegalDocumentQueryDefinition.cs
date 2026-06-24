using Granit.Privacy.Dtos;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.QueryEngine;
using Granit.Workflow.Domain;

namespace Granit.Privacy.Queries;

/// <summary>
/// Query definition for legal document admin management — declares columns, filters,
/// sorting, quick filters, and the list-view projection. Powers
/// <c>MapGranitQuery&lt;LegalDocument&gt;</c> on the admin endpoints.
/// </summary>
/// <remarks>
/// The <c>published</c> quick filter is the default so the list shows the active
/// version of each document on first load; opt in to <c>draft</c> or remove the
/// filter entirely to see all lifecycle statuses.
/// </remarks>
public sealed class LegalDocumentQueryDefinition : QueryDefinition<LegalDocument>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Privacy.LegalDocumentQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<LegalDocument> builder)
    {
        builder
            .Column(e => e.DocumentId, c => c
                .Label("Document")
                .LabelKey("Privacy.LegalDocuments.Columns.DocumentId")
                .Filterable()
                .Sortable())
            .Column(e => e.DisplayName, c => c
                .Label("Display Name")
                .LabelKey("Privacy.LegalDocuments.Columns.DisplayName")
                .Filterable()
                .Sortable())
            .Column(e => e.Version, c => c
                .Label("Version")
                .LabelKey("Privacy.LegalDocuments.Columns.Version")
                .Filterable()
                .Sortable())
            .Column(e => e.LifecycleStatus, c => c
                .Label("Status")
                .LabelKey("Privacy.LegalDocuments.Columns.LifecycleStatus")
                .Filterable()
                .Sortable())
            .Column(e => e.CreatedAt, c => c
                .Label("Created At")
                .LabelKey("Privacy.LegalDocuments.Columns.CreatedAt")
                .Sortable())
            .Column(e => e.ModifiedAt, c => c
                .Label("Modified At")
                .LabelKey("Privacy.LegalDocuments.Columns.LastModifiedAt")
                .Sortable())
            .QuickFilter(
                "draft",
                "Drafts",
                e => e.LifecycleStatus == WorkflowLifecycleStatus.Draft)
            .QuickFilter(
                "published",
                "Published",
                e => e.LifecycleStatus == WorkflowLifecycleStatus.Published,
                isDefault: true)
            .GlobalSearch(e => e.DocumentId)
            .DefaultSort("-modifiedAt")
            .DefaultPageSize(25)
            .MaxPageSize(100)
            .ProjectTo(e => new LegalDocumentDetailResponse(
                e.Id,
                e.DocumentId,
                e.Version,
                e.LifecycleStatus.ToString(),
                e.DisplayName,
                e.Description,
                e.TemplateName,
                e.DocumentBlobId,
                e.CreatedAt,
                e.ModifiedAt ?? e.CreatedAt,
                e.ConcurrencyStamp));
    }
}
