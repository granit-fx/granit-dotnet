using Granit.Wolverine.ClaimCheck;
using Granit.Wolverine.ClaimCheck.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Tests.ClaimCheck;

public sealed class ClaimCheckServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryClaimCheckStore_RegistersIClaimCheckStore_AsSingleton()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddInMemoryClaimCheckStore();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IClaimCheckStore) &&
            d.ImplementationType == typeof(InMemoryClaimCheckStore) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddInMemoryClaimCheckStore_ReturnsSameCollection_ForChaining()
    {
        IServiceCollection services = new ServiceCollection();

        IServiceCollection result = services.AddInMemoryClaimCheckStore();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddInMemoryClaimCheckStore_WhenAlreadyRegistered_DoesNotReplace()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IClaimCheckStore, FakeClaimCheckStore>();

        services.AddInMemoryClaimCheckStore();

        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IClaimCheckStore));
        descriptor.ImplementationType.ShouldBe(typeof(FakeClaimCheckStore));
    }

    private sealed class FakeClaimCheckStore : IClaimCheckStore
    {
        public Task<Guid> StoreAsync(ReadOnlyMemory<byte> data, string? contentType = null, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<byte[]?> RetrieveAsync(Guid referenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<bool> DeleteAsync(Guid referenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
