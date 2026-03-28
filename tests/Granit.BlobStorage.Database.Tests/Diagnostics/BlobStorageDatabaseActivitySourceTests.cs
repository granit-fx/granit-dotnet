using Granit.BlobStorage.Database.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Diagnostics;

public sealed class BlobStorageDatabaseActivitySourceTests
{
    [Fact]
    public void Name_IsCorrect() =>
        BlobStorageDatabaseActivitySource.Name.ShouldBe("Granit.BlobStorage.Database");

    [Fact]
    public void Source_IsNotNull() =>
        BlobStorageDatabaseActivitySource.Source.ShouldNotBeNull();

    [Fact]
    public void Source_HasCorrectName() =>
        BlobStorageDatabaseActivitySource.Source.Name.ShouldBe(BlobStorageDatabaseActivitySource.Name);

    [Fact]
    public void OperationNames_AreCorrect()
    {
        BlobStorageDatabaseActivitySource.Save.ShouldBe("blobstorage.save");
        BlobStorageDatabaseActivitySource.Read.ShouldBe("blobstorage.read");
        BlobStorageDatabaseActivitySource.Delete.ShouldBe("blobstorage.delete");
        BlobStorageDatabaseActivitySource.GetSize.ShouldBe("blobstorage.get-size");
        BlobStorageDatabaseActivitySource.PartialStream.ShouldBe("blobstorage.partial-stream");
    }

    [Fact]
    public void TagNames_AreCorrect()
    {
        BlobStorageDatabaseActivitySource.TagObjectKey.ShouldBe("blobstorage.object_key");
        BlobStorageDatabaseActivitySource.TagContentType.ShouldBe("blobstorage.content_type");
    }
}
