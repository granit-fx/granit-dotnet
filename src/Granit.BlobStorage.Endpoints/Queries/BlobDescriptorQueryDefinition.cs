using Granit.BlobStorage.Domain;
using Granit.QueryEngine;

namespace Granit.BlobStorage.Endpoints.Queries;

/// <summary>
/// Query definition for blob descriptors — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class BlobDescriptorQueryDefinition : QueryDefinition<BlobDescriptor>
{
    /// <inheritdoc/>
    public override string Name => "BlobStorage.BlobDescriptors";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<BlobDescriptor> builder)
    {
        builder
            .Column(b => b.TenantId, c => c.Label("Tenant").Filterable().Sortable())
            .Column(b => b.ContainerName, c => c.Label("Container").Filterable().Sortable())
            .Column(b => b.OriginalFileName, c => c.Label("File Name").Filterable().Sortable())
            .Column(b => b.DeclaredContentType, c => c.Label("Content Type").Filterable().Sortable())
            .Column(b => b.VerifiedContentType, c => c.Label("Verified Content Type").Filterable())
            .Column(b => b.SizeBytes, c => c.Label("Size (bytes)").Sortable())
            .Column(b => b.Status, c => c.Label("Status").Filterable().Sortable())
            .Column(b => b.CreatedAt, c => c.Label("Created At").Sortable())
            .Column(b => b.ValidatedAt, c => c.Label("Validated At").Sortable())
            .Column(b => b.DeletedAt, c => c.Label("Deleted At").Sortable())
            .Column(b => b.RejectionReason, c => c.Label("Rejection Reason"))
            .GlobalSearch(b => b.OriginalFileName, b => b.ContainerName)
            .DateFilter(b => b.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
