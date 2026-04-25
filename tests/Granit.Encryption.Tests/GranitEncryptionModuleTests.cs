// =============================================================================
// GranitEncryptionModuleTests - Module registration and DI wiring
// =============================================================================
// Verifies:
//   - Module inherits from GranitModule
//   - ConfigureServices registers all expected services
//   - No [DependsOn] attributes (zero-dependency module)
// =============================================================================

using Granit.Encryption.Diagnostics;
using Granit.Encryption.Extensions;
using Granit.Encryption.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class GranitEncryptionModuleTests : IDisposable
{
    private readonly ServiceProvider _sp;

    public GranitEncryptionModuleTests()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Services.AddMetrics();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);

        // Allow ephemeral passphrase so AES provider works without Vault.
        // Combined with the Development environment above, this passes the
        // production guard introduced in AesStringEncryptionProvider.
        builder.Services.Configure<StringEncryptionOptions>(opts =>
            opts.AllowEphemeralPassPhrase = true);

        GranitEncryptionModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        _sp = builder.Services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    // ──── Type hierarchy ────

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitEncryptionModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitEncryptionModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_HasNoDependsOnAttributes()
    {
        object[] attributes = typeof(GranitEncryptionModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldBeEmpty();
    }

    // ──── Service registrations ────

    [Fact]
    public void ConfigureServices_RegistersStringEncryptionService()
    {
        IStringEncryptionService service = _sp.GetRequiredService<IStringEncryptionService>();

        service.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersStringEncryptionProvider()
    {
        IStringEncryptionProvider provider = _sp.GetRequiredService<IStringEncryptionProvider>();

        provider.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersEntityEncryptionKeyStore()
    {
        using IServiceScope scope = _sp.CreateScope();
        IEntityEncryptionKeyStore store = scope.ServiceProvider.GetRequiredService<IEntityEncryptionKeyStore>();

        store.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersCryptoShredder()
    {
        using IServiceScope scope = _sp.CreateScope();
        ICryptoShredder shredder = scope.ServiceProvider.GetRequiredService<ICryptoShredder>();

        shredder.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersEncryptionMetrics()
    {
        EncryptionMetrics metrics = _sp.GetRequiredService<EncryptionMetrics>();

        metrics.ShouldNotBeNull();
    }
}
