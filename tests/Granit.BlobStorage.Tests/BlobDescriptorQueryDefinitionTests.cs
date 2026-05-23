using System.Linq.Expressions;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Queries;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobDescriptorQueryDefinitionTests
{
    [Fact]
    public void Definition_DeclaresValidOnlyQuickFilter_ActiveByDefault()
    {
        // Guards the UI default: Pending/Rejected/Deleted blobs should NOT appear in the admin
        // listing unless an admin explicitly toggles the filter off. Regression here re-exposes
        // orphaned uploads (NaN GB rows from failed presigned PUTs).
        BlobDescriptorQueryDefinition definition = new();

        QuickFilterDescriptor filter = definition.GetQuickFilters().ShouldHaveSingleItem();

        filter.Name.ShouldBe("ValidOnly");
        filter.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void ValidOnlyQuickFilter_PredicateMatchesValidStatusOnly()
    {
        BlobDescriptorQueryDefinition definition = new();
        QuickFilterDescriptor filter = definition.GetQuickFilters().Single();
        Func<BlobDescriptor, bool> predicate = ((Expression<Func<BlobDescriptor, bool>>)filter.Predicate).Compile();

        predicate(BuildDescriptor(BlobStatus.Valid)).ShouldBeTrue();
        predicate(BuildDescriptor(BlobStatus.Pending)).ShouldBeFalse();
        predicate(BuildDescriptor(BlobStatus.Uploading)).ShouldBeFalse();
        predicate(BuildDescriptor(BlobStatus.Rejected)).ShouldBeFalse();
        predicate(BuildDescriptor(BlobStatus.Deleted)).ShouldBeFalse();
    }

    private static BlobDescriptor BuildDescriptor(BlobStatus status)
    {
        var descriptor = BlobDescriptor.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            containerName: "documents",
            objectKey: "tenant/documents/2026/05/abc",
            request: new BlobUploadRequest("f.pdf", "application/pdf", 1024),
            createdAt: DateTimeOffset.UtcNow);

        DateTimeOffset later = DateTimeOffset.UtcNow.AddMinutes(5);
        switch (status)
        {
            case BlobStatus.Pending:
                break;
            case BlobStatus.Uploading:
                descriptor.MarkAsUploading();
                break;
            case BlobStatus.Valid:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("application/pdf", 1024, later);
                break;
            case BlobStatus.Rejected:
                descriptor.MarkAsUploading();
                descriptor.MarkAsRejected("test");
                break;
            case BlobStatus.Deleted:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("application/pdf", 1024, later);
                descriptor.MarkAsDeleted(later);
                break;
        }
        return descriptor;
    }
}
