// ====================================================================================
// AuditingEntityFrameworkCoreHostApplicationBuilderExtensionsTests — DI registration
// ====================================================================================
// Verifies:
//   - AddGranitAuditingEntityFrameworkCore registers all expected services
//   - AuditPersistencePipeline replaces the deleted channel/publisher/persister stack
//   - Returns builder for chaining
//   - TryAdd semantics for metrics and IHttpContextAccessor
// =============================================================================

using Granit.Auditing.Diagnostics;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Extensions;
using Granit.Events;
using Granit.Guids.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Extensions;

public sealed class AuditingEntityFrameworkCoreHostApplicationBuilderExtensionsTests : IDisposable
{
    private readonly HostApplicationBuilder _builder;
    private readonly ServiceProvider _sp;

    public AuditingEntityFrameworkCoreHostApplicationBuilderExtensionsTests()
    {
        _builder = Host.CreateEmptyApplicationBuilder(null);
        _builder.Services.AddMetrics();
        _builder.Services.AddLogging();
        _builder.Services.AddSingleton(TimeProvider.System);

        // Real hosts wire these via the module system (GranitPersistenceModule /
        // GranitGuidsModule) — the persistence pipeline depends on
        // IIntegrationEventDispatcher and IGuidGenerator.
        _builder.Services.AddSingleton(Substitute.For<IIntegrationEventDispatcher>());
        _builder.Services.AddGranitGuids();

        _builder.Services.AddGranitAuditing();
        _builder.AddGranitAuditingEntityFrameworkCore(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _sp = _builder.Services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    // =========================================================================
    // Chaining
    // =========================================================================

    [Fact]
    public void AddGranitAuditingEntityFrameworkCore_ReturnsBuilder()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddGranitAuditing();
        IHostApplicationBuilder result = builder.AddGranitAuditingEntityFrameworkCore(
            options => options.UseInMemoryDatabase("chaining-test"));

        result.ShouldBeSameAs(builder);
    }

    // =========================================================================
    // Service registrations — descriptors
    // =========================================================================

    [Fact]
    public void Registers_AuditingMetrics_AsSingleton()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(AuditingMetrics));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Registers_AuditPersistencePipeline_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(AuditPersistencePipeline));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registers_IAuditingReader_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IAuditingReader));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        descriptor.ImplementationType.ShouldBe(typeof(EfCoreAuditingReader));
    }

    [Fact]
    public void Registers_IAuditingWriter_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IAuditingWriter));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        descriptor.ImplementationType.ShouldBe(typeof(EfCoreAuditingWriter));
    }

    [Fact]
    public void Registers_IAuditingCleaner_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IAuditingCleaner));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        descriptor.ImplementationType.ShouldBe(typeof(EfCoreAuditingCleaner));
    }

    [Fact]
    public void Registers_AuditingChangeTrackingInterceptor_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(AuditingChangeTrackingInterceptor));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registers_IGranitAutoInterceptor_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IGranitAutoInterceptor));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Resolves_IGranitAutoInterceptor_AsAuditingChangeTrackingInterceptor()
    {
        using IServiceScope scope = _sp.CreateScope();
        IGranitAutoInterceptor interceptor = scope.ServiceProvider
            .GetRequiredService<IGranitAutoInterceptor>();

        interceptor.ShouldBeOfType<AuditingChangeTrackingInterceptor>();
    }

    [Fact]
    public void Registers_ChangeTrackingCaptureService_AsScoped()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(ChangeTrackingCaptureService));

        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registers_IHttpContextAccessor()
    {
        ServiceDescriptor? descriptor = _builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IHttpContextAccessor));

        descriptor.ShouldNotBeNull();
    }

    // =========================================================================
    // Resolvable services
    // =========================================================================

    [Fact]
    public void Resolves_AuditingMetrics()
    {
        AuditingMetrics metrics = _sp.GetRequiredService<AuditingMetrics>();

        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void Resolves_AuditPersistencePipeline()
    {
        using IServiceScope scope = _sp.CreateScope();
        AuditPersistencePipeline pipeline = scope.ServiceProvider
            .GetRequiredService<AuditPersistencePipeline>();

        pipeline.ShouldNotBeNull();
    }

    // =========================================================================
    // TryAdd semantics — idempotent registrations
    // =========================================================================

    [Fact]
    public void CallingTwice_DoesNotDuplicateMetrics()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddGranitAuditing();
        builder.AddGranitAuditingEntityFrameworkCore(
            o => o.UseInMemoryDatabase("dup-1"));
        builder.AddGranitAuditingEntityFrameworkCore(
            o => o.UseInMemoryDatabase("dup-2"));

        int metricsCount = builder.Services
            .Count(d => d.ServiceType == typeof(AuditingMetrics));

        metricsCount.ShouldBe(1);
    }

    [Fact]
    public void CallingTwice_DoesNotDuplicatePipeline()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddGranitAuditing();
        builder.AddGranitAuditingEntityFrameworkCore(
            o => o.UseInMemoryDatabase("dup-pipeline-1"));
        builder.AddGranitAuditingEntityFrameworkCore(
            o => o.UseInMemoryDatabase("dup-pipeline-2"));

        int pipelineCount = builder.Services
            .Count(d => d.ServiceType == typeof(AuditPersistencePipeline));

        pipelineCount.ShouldBe(1);
    }

    [Fact]
    public void DoesNotReplace_ExistingHttpContextAccessor()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);

        IHttpContextAccessor existing = new HttpContextAccessor();
        builder.Services.AddSingleton(existing);

        builder.Services.AddGranitAuditing();
        builder.AddGranitAuditingEntityFrameworkCore(
            o => o.UseInMemoryDatabase("existing-accessor"));

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        IHttpContextAccessor resolved = sp.GetRequiredService<IHttpContextAccessor>();

        resolved.ShouldBeSameAs(existing);
    }
}
