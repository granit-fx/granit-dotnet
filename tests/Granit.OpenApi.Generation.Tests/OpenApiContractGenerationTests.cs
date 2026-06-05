using Granit.OpenApi.Generation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.OpenApi.Generation.Tests;

public sealed class OpenApiContractGenerationTests
{
    [Fact]
    public void AddContractServiceStubs_registers_each_contract()
    {
        ServiceCollection services = new();

        services.AddContractServiceStubs(typeof(IComparable), typeof(IFormattable));

        services.ShouldContain(d => d.ServiceType == typeof(IComparable));
        services.ShouldContain(d => d.ServiceType == typeof(IFormattable));
    }

    [Fact]
    public void AddContractServiceStubs_returns_the_same_collection_for_chaining()
    {
        ServiceCollection services = new();

        services.AddContractServiceStubs(typeof(IComparable)).ShouldBeSameAs(services);
    }

    [Fact]
    public void AddContractServiceStubs_with_no_contracts_is_a_no_op()
    {
        ServiceCollection services = new();

        services.AddContractServiceStubs();

        services.ShouldBeEmpty();
    }

    [Fact]
    public void OpenApiContractModule_exposes_its_slug_and_invokes_the_map_action()
    {
        bool mapped = false;
        OpenApiContractModule module = new("blob-storage", _ => mapped = true);

        module.Slug.ShouldBe("blob-storage");
        module.Map(null!);

        mapped.ShouldBeTrue();
    }
}
