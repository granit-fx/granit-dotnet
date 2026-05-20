using Granit.Http.SecurityHeaders.Contributors;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class CspContributorRegistryTests
{
    [Fact]
    public void Add_RegistersContributor()
    {
        CspContributorRegistry registry = new();
        FakeContributor c = new();

        registry.Add(c);

        registry.Contributors.ShouldContain(c);
    }

    [Fact]
    public void Add_Idempotent_OnReferenceEquality()
    {
        CspContributorRegistry registry = new();
        FakeContributor c = new();

        registry.Add(c);
        registry.Add(c);
        registry.Add(c);

        registry.Contributors.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_DistinctInstances_KeptSeparately()
    {
        CspContributorRegistry registry = new();
        FakeContributor c1 = new();
        FakeContributor c2 = new();

        registry.Add(c1);
        registry.Add(c2);

        registry.Contributors.Count.ShouldBe(2);
    }

    [Fact]
    public void Add_Null_Throws()
    {
        CspContributorRegistry registry = new();

        Should.Throw<ArgumentNullException>(() => registry.Add(null!));
    }

    [Fact]
    public void Add_AfterFirstContributorsRead_Throws()
    {
        CspContributorRegistry registry = new();
        FakeContributor c1 = new();
        registry.Add(c1);

        // Reading Contributors locks the registry.
        _ = registry.Contributors;

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => registry.Add(new FakeContributor()));
        ex.Message.ShouldContain("locked");
        ex.Message.ShouldContain("UseGranitXxx");
    }

    [Fact]
    public void Contributors_OrderMatchesRegistrationOrder()
    {
        CspContributorRegistry registry = new();
        FakeContributor c1 = new();
        FakeContributor c2 = new();
        FakeContributor c3 = new();

        registry.Add(c1);
        registry.Add(c2);
        registry.Add(c3);

        registry.Contributors.ShouldBe([c1, c2, c3]);
    }

    [Fact]
    public void Contributors_RepeatedAccess_ReturnsSameSnapshot()
    {
        CspContributorRegistry registry = new();
        registry.Add(new FakeContributor());

        IReadOnlyCollection<ICspContributor> first = registry.Contributors;
        IReadOnlyCollection<ICspContributor> second = registry.Contributors;

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Add_FromMultipleThreads_IsThreadSafe()
    {
        CspContributorRegistry registry = new();
        const int N = 100;
        ICspContributor[] contributors = [.. Enumerable.Range(0, N).Select(_ => new FakeContributor())];

        Parallel.ForEach(contributors, registry.Add);

        registry.Contributors.Count.ShouldBe(N);
    }

    private sealed class FakeContributor : ICspContributor
    {
        public void Contribute(HttpContext context, CspBuilder builder) { }
    }
}
