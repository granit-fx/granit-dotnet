// =============================================================================
// Tests - GranitWolverineEncryptionModule
// =============================================================================
// Verifies module inheritance, DependsOn declarations, that ConfigureServices
// registers the IWolverineExtension applied at Wolverine bootstrap, and that
// mis-ordering (no AddGranitWolverine first) fails fast instead of silently
// shipping plaintext PII to the outbox.
// =============================================================================

using Granit.Encryption;
using Granit.Modularity;
using Granit.Wolverine.Encryption.Internal;
using Granit.Wolverine.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Encryption.Tests;

public sealed class GranitWolverineEncryptionModuleTests
{
    [Fact]
    public void GranitWolverineEncryptionModule_IsGranitModule() =>
        typeof(GranitWolverineEncryptionModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitWolverineEncryptionModule_IsSealed() =>
        typeof(GranitWolverineEncryptionModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void GranitWolverineEncryptionModule_DependsOn_WolverineAndEncryptionModules()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverineEncryptionModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitWolverineModule)));
        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitEncryptionModule)));
    }

    [Fact]
    public void ConfigureServices_RegistersWolverineEncryptionExtension()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();
        GranitWolverineEncryptionModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWolverineExtension)
            && d.ImplementationType == typeof(WolverineEncryptionExtension));
    }

    [Fact]
    public void ConfigureServices_WithoutWolverineModule_ThrowsInvalidOperationException()
    {
        // Without AddGranitWolverine() the extension would never be applied and
        // [Encrypted] properties would reach the outbox in plaintext — fail fast.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitWolverineEncryptionModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        Action act = () => module.ConfigureServices(context);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("GranitWolverineModule");
    }
}
