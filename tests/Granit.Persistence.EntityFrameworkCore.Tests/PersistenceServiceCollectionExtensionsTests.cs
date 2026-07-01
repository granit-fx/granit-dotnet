// =============================================================================
// Tests - PersistenceServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitPersistence registers the EF Core interceptors
// and the IDataFilter service.
// Verifies that AddGranitDbContextHealthCheck<T> registers a readiness health check.
// =============================================================================

using Granit.DataFiltering;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class PersistenceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitPersistence_RegistersAuditedEntityInterceptor()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        AuditedEntityInterceptor? interceptor = scope.ServiceProvider.GetService<AuditedEntityInterceptor>();
        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_RegistersSoftDeleteInterceptor()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        SoftDeleteInterceptor? interceptor = scope.ServiceProvider.GetService<SoftDeleteInterceptor>();
        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_RegistersScoped_TenantQueryScope()
    {
        // VULN-001: the QueryEngine-path guard must be resolvable and Scoped (it reads the
        // request-local ICurrentTenant / IHostAccessContext live per call).
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitPersistence();

        ServiceDescriptor descriptor = services.First(d => d.ServiceType == typeof(ITenantQueryScope));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        scope.ServiceProvider.GetService<ITenantQueryScope>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_RegistersVersioningInterceptor()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        VersioningInterceptor? interceptor = scope.ServiceProvider.GetService<VersioningInterceptor>();
        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_RegistersConcurrencyStampInterceptor()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        ConcurrencyStampInterceptor? interceptor = scope.ServiceProvider.GetService<ConcurrencyStampInterceptor>();
        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_InterceptorsAreScoped()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitPersistence();

        // Assert
        ServiceDescriptor auditDescriptor = services.First(d => d.ServiceType == typeof(AuditedEntityInterceptor));
        auditDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        ServiceDescriptor versioningDescriptor = services.First(d => d.ServiceType == typeof(VersioningInterceptor));
        versioningDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        ServiceDescriptor concurrencyStampDescriptor = services.First(d => d.ServiceType == typeof(ConcurrencyStampInterceptor));
        concurrencyStampDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        ServiceDescriptor softDeleteDescriptor = services.First(d => d.ServiceType == typeof(SoftDeleteInterceptor));
        softDeleteDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitPersistence_RegistersDomainEventDispatcherInterceptor()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        DomainEventDispatcherInterceptor? interceptor = scope.ServiceProvider.GetService<DomainEventDispatcherInterceptor>();
        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_RegistersNullDomainEventDispatcher_ByDefault()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IDomainEventDispatcher? dispatcher = sp.GetService<IDomainEventDispatcher>();
        dispatcher.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPersistence_DoesNotOverrideExistingDispatcher()
    {
        // Arrange
        ServiceCollection services = new();
        IDomainEventDispatcher custom = NSubstitute.Substitute.For<IDomainEventDispatcher>();
        services.AddSingleton(custom);

        // Act
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — the custom dispatcher should be preserved (TryAdd)
        IDomainEventDispatcher? resolved = sp.GetService<IDomainEventDispatcher>();
        resolved.ShouldBeSameAs(custom);
    }

    [Fact]
    public void AddGranitPersistence_RegistersDataFilter_AsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitPersistence();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataFilter));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationType.ShouldBe(typeof(DataFilter));
    }

    [Fact]
    public void AddGranitDbContextHealthCheck_WithDefaultName_RegistersCheckNamedAfterDbContextType()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(opts => opts.UseInMemoryDatabase("test-health"));
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act — no explicit name → defaults to typeof(TContext).Name
        builder.AddGranitDbContextHealthCheck<TestDbContext>();

        // Assert — registration uses type name and is tagged "readiness" and "startup"
        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? registration = opts.Registrations.FirstOrDefault(
            r => r.Name == nameof(TestDbContext));
        registration.ShouldNotBeNull();
        registration.Tags.ShouldContain("readiness");
        registration.Tags.ShouldContain("startup");
    }

    [Fact]
    public void AddGranitDbContextHealthCheck_WithCustomName_RegistersCheckWithThatName()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(opts => opts.UseInMemoryDatabase("test-health-custom"));
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act — explicit name
        builder.AddGranitDbContextHealthCheck<TestDbContext>(name: "database");

        // Assert
        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? registration = opts.Registrations.FirstOrDefault(r => r.Name == "database");
        registration.ShouldNotBeNull();
        registration.Tags.ShouldContain("readiness");
    }

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        // AuditedEntityInterceptor requires IClock, IGuidGenerator, ICurrentUserService, ICurrentTenant
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Timing.IClock>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Guids.IGuidGenerator>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(NSubstitute.Substitute.For<ICurrentTenant>());
        services.AddLogging();
        services.AddMetrics();
    }

    /// <summary>Minimal DbContext for health check registration tests.</summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
