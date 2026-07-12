// =============================================================================
// EmbeddedAuditingTestHarness - shared fixture for embedded-mode pipeline tests
// =============================================================================
// Composes the REAL embedded auditing stack the way a production host does:
//   - a host DbContext that maps a business entity AND the audit entities via
//     modelBuilder.ConfigureAuditingModule() (embedded mode),
//   - the real AuditingChangeTrackingInterceptor added to the options,
//   - the scoped ChangeTrackingCaptureService + AuditPersistencePipeline living
//     in an application ServiceProvider bridged to EF Core through
//     UseApplicationServiceProvider (exactly what UseGranitInterceptors wires),
//   - the pipeline's IDbContextFactory<AuditingDbContext> pointed at a SEPARATE
//     (decoy) Sqlite database, so tests can prove the embedded path never takes
//     the standalone route (the decoy store must stay empty).
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Auditing.Attributes;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Options;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

internal sealed class EmbeddedAuditingTestHarness : IDisposable
{
    private readonly SqliteConnection _hostConnection;
    private readonly SqliteConnection _standaloneConnection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly DbContextOptions<EmbeddedHostDbContext> _auditedHostOptions;
    private readonly DbContextOptions<EmbeddedHostDbContext> _plainHostOptions;

    public IClock Clock { get; } = Substitute.For<IClock>();
    public ICurrentUserService CurrentUserService { get; } = Substitute.For<ICurrentUserService>();
    public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
    public IIntegrationEventDispatcher EventDispatcher { get; } = Substitute.For<IIntegrationEventDispatcher>();
    public AuditingOptions Options { get; } = new();
    public RecordingMeterFactory MeterFactory { get; } = new();
    public AuditingMetrics Metrics { get; }
    public SimpleGuidGenerator GuidGenerator { get; } = new();

    /// <summary>Options of the isolated (decoy) standalone audit store.</summary>
    public DbContextOptions<AuditingDbContext> StandaloneDbOptions { get; }

    /// <summary>The application scope EF Core is bridged to — resolve the scoped pipeline/capture service here.</summary>
    public IServiceProvider ScopeServices => _scope.ServiceProvider;

    public EmbeddedAuditingTestHarness(Guid? tenantId = null)
    {
        Clock.Now.Returns(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
        CurrentUserService.UserId.Returns("test-user");
        CurrentUserService.UserName.Returns("Test User");
        CurrentTenant.IsAvailable.Returns(tenantId is not null);
        CurrentTenant.Id.Returns(tenantId);

        Metrics = new AuditingMetrics(MeterFactory);

        // Decoy standalone store: schema created, must stay empty on the embedded path.
        _standaloneConnection = new SqliteConnection("DataSource=:memory:");
        _standaloneConnection.Open();
        StandaloneDbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseSqlite(_standaloneConnection)
            .Options;
        using (AuditingDbContext standalone = new(StandaloneDbOptions, GranitDesignTime.CurrentTenant))
        {
            standalone.Database.EnsureCreated();
        }

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(Clock);
        services.AddSingleton(CurrentUserService);
        services.AddSingleton(CurrentTenant);
        services.AddSingleton(EventDispatcher);
        services.AddSingleton<IGuidGenerator>(GuidGenerator);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton<IMeterFactory>(MeterFactory);
        services.AddSingleton(Metrics);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IDbContextFactory<AuditingDbContext>>(
            new StubAuditingDbContextFactory(StandaloneDbOptions));
        services.AddScoped<AuditPersistencePipeline>();
        services.AddScoped<ChangeTrackingCaptureService>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        _hostConnection = new SqliteConnection("DataSource=:memory:");
        _hostConnection.Open();

        _auditedHostOptions = new DbContextOptionsBuilder<EmbeddedHostDbContext>()
            .UseSqlite(_hostConnection)
            .AddInterceptors(new AuditingChangeTrackingInterceptor())
            .UseApplicationServiceProvider(_scope.ServiceProvider)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        // Plain options: same database, no interceptor, no application SP — used for
        // schema creation, seeding without audit capture, and verification reads.
        _plainHostOptions = new DbContextOptionsBuilder<EmbeddedHostDbContext>()
            .UseSqlite(_hostConnection)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        using EmbeddedHostDbContext setup = new(_plainHostOptions);
        setup.Database.EnsureCreated();
    }

    /// <summary>Host context with the real interceptor and the application SP bridge (embedded mode).</summary>
    public EmbeddedHostDbContext CreateAuditedHostContext() => new(_auditedHostOptions);

    /// <summary>Host context over the same database WITHOUT audit capture (seeding/verification).</summary>
    public EmbeddedHostDbContext CreatePlainHostContext() => new(_plainHostOptions);

    /// <summary>Audit entries written into the HOST database (embedded path evidence).</summary>
    public async Task<List<AuditEntry>> GetHostAuditEntriesAsync()
    {
        await using EmbeddedHostDbContext context = CreatePlainHostContext();
        return await context.Set<AuditEntry>()
            .AsNoTracking()
            .Include(e => e.EntityChanges)
            .ThenInclude(c => c.PropertyChanges)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Audit entries written into the DECOY standalone store (must stay empty in embedded mode).</summary>
    public async Task<List<AuditEntry>> GetStandaloneAuditEntriesAsync()
    {
        await using AuditingDbContext context = new(StandaloneDbOptions, GranitDesignTime.CurrentTenant);
        return await context.AuditEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _hostConnection.Dispose();
        _standaloneConnection.Dispose();
        MeterFactory.Dispose();
    }
}

/// <summary>
/// Embedded audited host context: business entities plus the audit entities mapped via
/// <c>ConfigureAuditingModule()</c>, exactly like a production host opting into atomic auditing.
/// </summary>
internal sealed class EmbeddedHostDbContext(DbContextOptions<EmbeddedHostDbContext> options)
    : DbContext(options)
{
    public DbSet<HostCustomer> Customers => Set<HostCustomer>();
    public DbSet<AuditIgnoredHostEntity> IgnoredEntities => Set<AuditIgnoredHostEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Unique constraint used to force a commit failure deterministically.
        modelBuilder.Entity<HostCustomer>().HasIndex(c => c.Name).IsUnique();
        modelBuilder.ConfigureAuditingModule();
    }
}

internal sealed class HostCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[AuditIgnore]
internal sealed class AuditIgnoredHostEntity
{
    public int Id { get; set; }
    public string Payload { get; set; } = string.Empty;
}

/// <summary>Factory handing out AuditingDbContext instances over fixed options (standalone store).</summary>
internal sealed class StubAuditingDbContextFactory(
    DbContextOptions<AuditingDbContext> options,
    ICurrentTenant? currentTenant = null)
    : IDbContextFactory<AuditingDbContext>
{
    public AuditingDbContext CreateDbContext() =>
        new(options, currentTenant ?? GranitDesignTime.CurrentTenant);
}

/// <summary>
/// IMeterFactory that keeps the meters it created, so a MeterListener can enable
/// measurement events for THIS test's instruments only — parallel test classes create
/// meters with the same name ("Granit.Auditing") and would otherwise cross-pollute counts.
/// </summary>
internal sealed class RecordingMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public IReadOnlyList<Meter> Meters => _meters;

    public Meter Create(MeterOptions options)
    {
        Meter meter = new(options);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (Meter meter in _meters)
        {
            meter.Dispose();
        }
    }
}
