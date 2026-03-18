using Granit.Core.Extensions;
using Granit.Core.Modularity;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Testing;

/// <summary>
/// Test fixture that bootstraps a full Granit module graph with configurable fakes.
/// </summary>
/// <remarks>
/// <para>
/// Builds a real DI container by calling <c>AddGranit&lt;TModule&gt;()</c>,
/// then replaces the four core services with AsyncLocal-backed fakes. This gives
/// tests a realistic module graph while keeping each test isolated.
/// </para>
/// <para>
/// <b>Usage as <c>IClassFixture&lt;T&gt;</c> (recommended):</b>
/// The fixture is built once per test class and shared across all tests.
/// Because fakes use <see cref="AsyncLocal{T}"/>, mutations in one test
/// are invisible to other tests running in parallel.
/// </para>
/// <para>
/// <b>Usage per-test:</b> Create the fixture in the test constructor for
/// full isolation at the cost of slower execution.
/// </para>
/// </remarks>
/// <typeparam name="TModule">Root module to bootstrap.</typeparam>
public class GranitTestFixture<TModule> : IAsyncDisposable, IDisposable
    where TModule : GranitModule
{
    private IHost? _host;

    /// <summary>Configurable tenant context for tests.</summary>
    public FakeCurrentTenant Tenant { get; } = new();

    /// <summary>Configurable user identity for tests.</summary>
    public FakeCurrentUser User { get; } = new();

    /// <summary>Configurable clock for tests.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>Configurable GUID generator for tests.</summary>
    public FakeGuidGenerator GuidGenerator { get; } = new();

    /// <summary>
    /// The built service provider. Throws if <see cref="BuildAsync"/> has not been called.
    /// </summary>
    public IServiceProvider ServiceProvider => _host?.Services
        ?? throw new InvalidOperationException(
            $"Call {nameof(BuildAsync)}() before accessing {nameof(ServiceProvider)}.");

    /// <summary>
    /// Builds the host with the Granit module graph and replaces core services with fakes.
    /// </summary>
    /// <param name="configureServices">
    /// Optional callback to register additional test-specific services
    /// (e.g. test DbContext, additional mocks).
    /// </param>
    public async Task BuildAsync(Action<IServiceCollection>? configureServices = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        await builder.AddGranitAsync<TModule>().ConfigureAwait(false);

        builder.Services.Replace(ServiceDescriptor.Singleton<ICurrentTenant>(Tenant));
        builder.Services.Replace(ServiceDescriptor.Singleton<ICurrentUserService>(User));
        builder.Services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
        builder.Services.Replace(ServiceDescriptor.Singleton<IGuidGenerator>(GuidGenerator));

        configureServices?.Invoke(builder.Services);

        _host = builder.Build();
        await _host.UseGranitAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a required service from the built container.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull =>
        ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Resolves an optional service from the built container.
    /// </summary>
    public T? GetService<T>() => ServiceProvider.GetService<T>();

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
            _host = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
            _host = null;
        }
    }
}
