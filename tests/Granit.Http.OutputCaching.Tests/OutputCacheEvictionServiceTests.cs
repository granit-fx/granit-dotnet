using Granit.Http.OutputCaching.Eviction;
using Granit.Http.OutputCaching.Policies;
using Microsoft.AspNetCore.OutputCaching;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class OutputCacheEvictionServiceTests
{
    private readonly IOutputCacheStore _store = Substitute.For<IOutputCacheStore>();
    private readonly OutputCacheEvictionService _sut;

    public OutputCacheEvictionServiceTests()
    {
        _sut = new OutputCacheEvictionService(_store);
    }

    [Fact]
    public async Task EvictModuleCacheAsync_CallsStoreWithModuleName()
    {
        // Arrange
        const string moduleName = "Workflow";

        // Act
        await _sut.EvictModuleCacheAsync(moduleName, TestContext.Current.CancellationToken);

        // Assert
        await _store.Received(1).EvictByTagAsync(moduleName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvictModuleCacheAsync_WhenNullOrWhiteSpace_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.EvictModuleCacheAsync("", TestContext.Current.CancellationToken));

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.EvictModuleCacheAsync("   ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvictTenantCacheAsync_CallsStoreWithTenantTag()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        string expectedTag = $"{TenantAwareOutputCachePolicy.TenantTagPrefix}{tenantId}";

        // Act
        await _sut.EvictTenantCacheAsync(tenantId, TestContext.Current.CancellationToken);

        // Assert
        await _store.Received(1).EvictByTagAsync(expectedTag, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvictByTagAsync_CallsStoreWithTag()
    {
        // Arrange
        const string tag = "custom-tag";

        // Act
        await _sut.EvictByTagAsync(tag, TestContext.Current.CancellationToken);

        // Assert
        await _store.Received(1).EvictByTagAsync(tag, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvictByTagAsync_WhenNullOrWhiteSpace_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.EvictByTagAsync("", TestContext.Current.CancellationToken));
    }
}
