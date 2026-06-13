using Granit.Identity.Extensions;
using Granit.Identity.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

/// <summary>
/// Verifies that <see cref="IIdentityProvider"/> correctly inherits all fine-grained
/// interfaces and that DI registration forwards each one to the same provider instance.
/// </summary>
public sealed class IdentityInterfaceSegregationTests
{
    private static readonly Type[] FineGrainedInterfaces =
    [
        typeof(IIdentityUserReader),
        typeof(IIdentityUserWriter),
        typeof(IIdentityRoleManager),
        typeof(IIdentityGroupManager),
        typeof(IIdentityPasswordManager),
        typeof(IIdentityCredentialVerifier),
    ];

    // ───────────────────────────────────────────────
    // 1. IIdentityProvider inherits all 6 interfaces
    // ───────────────────────────────────────────────

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityUserReader() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityUserReader)).ShouldBeTrue();

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityUserWriter() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityUserWriter)).ShouldBeTrue();

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityRoleManager() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityRoleManager)).ShouldBeTrue();

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityGroupManager() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityGroupManager)).ShouldBeTrue();

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityPasswordManager() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityPasswordManager)).ShouldBeTrue();

    [Fact]
    public void IIdentityProvider_IsAssignableTo_IIdentityCredentialVerifier() =>
        typeof(IIdentityProvider).IsAssignableTo(typeof(IIdentityCredentialVerifier)).ShouldBeTrue();

    // ──────────────────────────────────────────────────────────────────
    // 2. NullIdentityProvider can be cast to each fine-grained interface
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void NullIdentityProvider_ImplementsIIdentityProvider()
    {
        NullIdentityProvider provider = new();

        provider.ShouldBeAssignableTo<IIdentityProvider>();
    }

    [Fact]
    public void NullIdentityProvider_CastableToAllFineGrainedInterfaces()
    {
        NullIdentityProvider provider = new();

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            interfaceType.IsInstanceOfType(provider).ShouldBeTrue(
                $"NullIdentityProvider should be assignable to {interfaceType.Name}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. AddGranitIdentity registers and resolves all fine-grained interfaces
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddGranitIdentity_ResolvesIIdentityProvider()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IIdentityProvider>()
            .ShouldBeOfType<NullIdentityProvider>();
    }

    [Fact]
    public void AddGranitIdentity_ResolvesAllFineGrainedInterfaces()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            object resolved = scope.ServiceProvider.GetRequiredService(interfaceType);
            resolved.ShouldNotBeNull(
                $"{interfaceType.Name} should be resolvable from the service provider");
        }
    }

    [Fact]
    public void AddGranitIdentity_AllFineGrainedInterfacesResolveSameInstance()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IIdentityProvider identityProvider = scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>();

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            object resolved = scope.ServiceProvider.GetRequiredService(interfaceType);
            resolved.ShouldBeSameAs(identityProvider,
                $"{interfaceType.Name} should resolve to the same instance as IIdentityProvider");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 4. AddIdentityProvider replaces and re-registers fine-grained interfaces
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddIdentityProvider_ReplacesDefaultWithCustomProvider()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();
        services.AddIdentityProvider<FakeIdentityProvider>();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IIdentityProvider>()
            .ShouldBeOfType<FakeIdentityProvider>();
    }

    [Fact]
    public void AddIdentityProvider_AllFineGrainedInterfacesResolveToReplacedProvider()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();
        services.AddIdentityProvider<FakeIdentityProvider>();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IIdentityProvider identityProvider = scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>();
        identityProvider.ShouldBeOfType<FakeIdentityProvider>();

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            object resolved = scope.ServiceProvider.GetRequiredService(interfaceType);
            resolved.ShouldBeSameAs(identityProvider,
                $"{interfaceType.Name} should resolve to the replaced FakeIdentityProvider instance");
        }
    }

    [Fact]
    public void AddIdentityProvider_WithNSubstituteMock_ResolvesAllInterfaces()
    {
        ServiceCollection services = new();
        IIdentityProvider mock = Substitute.For<IIdentityProvider>();

        // Register the mock as the IIdentityProvider, then wire fine-grained forwarding.
        services.AddScoped(_ => mock);
        services.AddGranitIdentity();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IIdentityProvider resolved = scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>();
        resolved.ShouldBeSameAs(mock);

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            object fineGrained = scope.ServiceProvider.GetRequiredService(interfaceType);
            fineGrained.ShouldBeSameAs(mock,
                $"{interfaceType.Name} should resolve to the NSubstitute mock");
        }
    }

    [Fact]
    public void AddIdentityProvider_FineGrainedInterfacesRegisteredAsScoped()
    {
        ServiceCollection services = new();
        services.AddGranitIdentity();

        foreach (Type interfaceType in FineGrainedInterfaces)
        {
            ServiceDescriptor? descriptor = services.FirstOrDefault(
                d => d.ServiceType == interfaceType);
            descriptor.ShouldNotBeNull(
                $"{interfaceType.Name} should be registered in the service collection");
            descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped,
                $"{interfaceType.Name} should be registered as Scoped");
        }
    }
}
