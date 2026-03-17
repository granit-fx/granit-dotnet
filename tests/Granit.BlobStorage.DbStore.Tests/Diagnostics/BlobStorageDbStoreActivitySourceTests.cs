using System.Diagnostics;
using Granit.BlobStorage.DbStore.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.DbStore.Tests.Diagnostics;

public sealed class BlobStorageDbStoreActivitySourceTests
{
    [Fact]
    public void Name_ShouldBe_GranitBlobStorageDbStore() =>
        BlobStorageDbStoreActivitySource.Name.ShouldBe("Granit.BlobStorage.DbStore");

    [Fact]
    public void Source_ShouldCreateActivity_WhenListenerIsRegistered()
    {
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BlobStorageDbStoreActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);

        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.Save);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe(BlobStorageDbStoreActivitySource.Save);
    }
}
