using Granit.Identity.Federated.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Options;

public sealed class UserCacheOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityFederatedUserCache() => UserCacheOptions.SectionName.ShouldBe("Identity:Federated:UserCache");

    [Fact]
    public void Defaults_StalenessThresholdIs24Hours()
    {
        UserCacheOptions options = new();

        options.StalenessThreshold.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Defaults_EnableLoginTimeSyncIsTrue()
    {
        UserCacheOptions options = new();

        options.EnableLoginTimeSync.ShouldBeTrue();
    }

    [Fact]
    public void Defaults_IncrementalSyncBatchSizeIs50()
    {
        UserCacheOptions options = new();

        options.IncrementalSyncBatchSize.ShouldBe(50);
    }
}
