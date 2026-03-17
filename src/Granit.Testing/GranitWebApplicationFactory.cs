using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Testing;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces core Granit services
/// with AsyncLocal-backed fakes for endpoint integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a real ASP.NET Core test server with the application's full middleware pipeline,
/// routing, and authentication. The four core Granit services are replaced with fakes
/// so tests can control tenant, user, clock, and GUID generation without touching
/// the real infrastructure.
/// </para>
/// <para>
/// Because fakes use <see cref="AsyncLocal{T}"/>, each test gets isolated state
/// even when the factory is shared via <c>IClassFixture&lt;T&gt;</c>.
/// </para>
/// </remarks>
/// <typeparam name="TProgram">
/// The application entry point type (typically the <c>Program</c> class
/// or any type in the web application assembly).
/// </typeparam>
public class GranitWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    /// <summary>Configurable tenant context for tests.</summary>
    public FakeCurrentTenant Tenant { get; } = new();

    /// <summary>Configurable user identity for tests.</summary>
    public FakeCurrentUser User { get; } = new();

    /// <summary>Configurable clock for tests.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>Configurable GUID generator for tests.</summary>
    public FakeGuidGenerator GuidGenerator { get; } = new();

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentTenant>(Tenant));
            services.Replace(ServiceDescriptor.Singleton<ICurrentUserService>(User));
            services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
            services.Replace(ServiceDescriptor.Singleton<IGuidGenerator>(GuidGenerator));
        });
    }
}
