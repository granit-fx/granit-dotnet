using Granit.Domain;
using Granit.Mergeable.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Mergeable.Tests.Extensions;

public sealed class MergeableServiceCollectionExtensionsTests
{
    [Fact]
    public void AddReferenceRewriter_RegistersRewriterAsScoped()
    {
        var services = new ServiceCollection();

        services.AddReferenceRewriter<FakeAggregate, FakeRewriter>();

        ServiceDescriptor descriptor = services.Single(d =>
            d.ServiceType == typeof(IReferenceRewriter<FakeAggregate>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        descriptor.ImplementationType.ShouldBe(typeof(FakeRewriter));
    }

    [Fact]
    public void AddReferenceRewriter_AllowsMultipleRewritersForSameAggregate()
    {
        var services = new ServiceCollection();

        services.AddReferenceRewriter<FakeAggregate, FakeRewriter>();
        services.AddReferenceRewriter<FakeAggregate, AnotherFakeRewriter>();

        services.BuildServiceProvider()
            .GetServices<IReferenceRewriter<FakeAggregate>>()
            .Count()
            .ShouldBe(2);
    }

    private sealed class FakeAggregate : AggregateRoot
    {
        public FakeAggregate() => Id = Guid.NewGuid();
    }

    private sealed class FakeRewriter : IReferenceRewriter<FakeAggregate>
    {
        public string Description => "fake.RefId";
        public Task<int> RewriteAsync(Guid s, Guid l, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountAsync(Guid s, Guid l, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class AnotherFakeRewriter : IReferenceRewriter<FakeAggregate>
    {
        public string Description => "another.RefId";
        public Task<int> RewriteAsync(Guid s, Guid l, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountAsync(Guid s, Guid l, CancellationToken ct) => Task.FromResult(0);
    }
}
