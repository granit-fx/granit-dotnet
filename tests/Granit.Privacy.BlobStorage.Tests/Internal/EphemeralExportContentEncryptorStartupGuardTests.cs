using Granit.Privacy.BlobStorage.Internal;
using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.Internal;

public sealed class EphemeralExportContentEncryptorStartupGuardTests
{
    [Fact]
    public async Task StartAsync_NonDevelopment_WithEphemeralEncryptor_FailsClosed()
    {
        // The whole point of the guard: a real deployment must NOT silently ship
        // personal-data exports encrypted with a throwaway per-process key. Startup aborts.
        using EphemeralExportContentEncryptor encryptor =
            new(NullLogger<EphemeralExportContentEncryptor>.Instance);
        EphemeralExportContentEncryptorStartupGuard guard = new(encryptor, Env("Production"));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(TestContext.Current.CancellationToken));
        ex.Message.ShouldContain(nameof(EphemeralExportContentEncryptor));
    }

    [Fact]
    public async Task StartAsync_Development_WithEphemeralEncryptor_Allowed()
    {
        // Local-only runs may use the ephemeral key — the guard is a no-op in Development.
        using EphemeralExportContentEncryptor encryptor =
            new(NullLogger<EphemeralExportContentEncryptor>.Instance);
        EphemeralExportContentEncryptorStartupGuard guard = new(encryptor, Env("Development"));

        await Should.NotThrowAsync(() => guard.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_NonDevelopment_WithProductionEncryptor_Allowed()
    {
        // A host that wired a shared-state (Vault-backed) encryptor passes the guard.
        IExportContentEncryptor production = Substitute.For<IExportContentEncryptor>();
        EphemeralExportContentEncryptorStartupGuard guard = new(production, Env("Production"));

        await Should.NotThrowAsync(() => guard.StartAsync(TestContext.Current.CancellationToken));
    }

    private static StubHostEnvironment Env(string name) => new() { EnvironmentName = name };

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
