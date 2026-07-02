using Granit.BlobStorage.Domain;
using Granit.QueryEngine;

namespace Granit.BlobStorage.Queries;

/// <summary>
/// Query definition for blob descriptors — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class BlobDescriptorQueryDefinition : QueryDefinition<BlobDescriptor>
{
    /// <inheritdoc/>
    public override string Name => "Granit.BlobStorage.BlobDescriptorsQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(BlobStorageLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<BlobDescriptor> builder)
    {
        builder
            .Column(b => b.TenantId, c => c.Label("Tenant").LabelKey("BlobStorage.Columns.Tenant").Filterable().Sortable())
            .Column(b => b.ContainerName, c => c.Label("Container").LabelKey("BlobStorage.Columns.Container").Filterable().Sortable())
            .Column(b => b.OriginalFileName, c => c.Label("File Name").LabelKey("BlobStorage.Columns.FileName").Filterable().Sortable())
            .Column(b => b.DeclaredContentType, c => c.Label("Content Type").LabelKey("BlobStorage.Columns.ContentType").Filterable().Sortable())
            .Column(b => b.VerifiedContentType, c => c.Label("Verified Content Type").LabelKey("BlobStorage.Columns.VerifiedContentType").Filterable())
            .Column(b => b.SizeBytes, c => c.Label("Size (bytes)").LabelKey("BlobStorage.Columns.SizeBytes").Sortable())
            .Column(b => b.Status, c => c.Label("Status").LabelKey("BlobStorage.Columns.Status").Filterable().Sortable())
            .Column(b => b.CreatedAt, c => c.Label("Created At").LabelKey("BlobStorage.Columns.CreatedAt").Sortable())
            .Column(b => b.ValidatedAt, c => c.Label("Validated At").LabelKey("BlobStorage.Columns.ValidatedAt").Sortable())
            .Column(b => b.DeletedAt, c => c.Label("Deleted At").LabelKey("BlobStorage.Columns.DeletedAt").Sortable())
            .Column(b => b.RejectionReason, c => c.Label("Rejection Reason").LabelKey("BlobStorage.Columns.RejectionReason"))
            .AllowGroupBy(b => b.Status)
            .QuickFilter(
                "ValidOnly",
                "Valid uploads only",
                b => b.Status == BlobStatus.Valid,
                isDefault: true)
            .GlobalSearch(b => b.OriginalFileName, b => b.ContainerName)
            .DateFilter(b => b.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
