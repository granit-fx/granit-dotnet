using Granit.Persistence.Interceptors;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.EntityFrameworkCore;

/// <summary>
/// Creates EF Core <typeparamref name="TContext"/> instances backed by
/// the EF Core In-Memory provider with Granit interceptors wired automatically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Prefer <see cref="SqliteDbContextFactory{TContext}"/> for most tests.</b>
/// The In-Memory provider does NOT enforce foreign key constraints, does not
/// support transactions, and does not translate LINQ to SQL. Use this factory
/// only for tests that exercise interceptor/change-tracker logic where SQL
/// semantics are irrelevant.
/// </para>
/// <para>
/// Each factory instance shares a single in-memory database name. Call
/// <see cref="CreateContext"/> multiple times to get contexts that share
/// the same data.
/// </para>
/// </remarks>
/// <typeparam name="TContext">
/// The <see cref="DbContext"/> type. Must have a constructor accepting
/// <see cref="DbContextOptions{TContext}"/>.
/// </typeparam>
public sealed class InMemoryDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly FakeCurrentTenant _tenant;
    private readonly FakeCurrentUser _user;
    private readonly FakeClock _clock;
    private readonly FakeGuidGenerator _guidGenerator;
    private readonly string _databaseName;
    private readonly Action<DbContextOptionsBuilder>? _configureOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDbContextFactory{TContext}"/> class.
    /// </summary>
    /// <param name="tenant">Fake tenant (defaults to a new <see cref="FakeCurrentTenant"/>).</param>
    /// <param name="user">Fake user (defaults to a new <see cref="FakeCurrentUser"/>).</param>
    /// <param name="clock">Fake clock (defaults to a new <see cref="FakeClock"/>).</param>
    /// <param name="guidGenerator">Fake GUID generator (defaults to a new <see cref="FakeGuidGenerator"/>).</param>
    /// <param name="configureOptions">Optional callback for additional <see cref="DbContextOptionsBuilder"/> configuration.</param>
    public InMemoryDbContextFactory(
        FakeCurrentTenant? tenant = null,
        FakeCurrentUser? user = null,
        FakeClock? clock = null,
        FakeGuidGenerator? guidGenerator = null,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        _tenant = tenant ?? new FakeCurrentTenant();
        _user = user ?? new FakeCurrentUser();
        _clock = clock ?? new FakeClock();
        _guidGenerator = guidGenerator ?? new FakeGuidGenerator();
#pragma warning disable GRSEC002 // DB name needs uniqueness, not sequential generation
        _databaseName = Guid.NewGuid().ToString();
#pragma warning restore GRSEC002
        _configureOptions = configureOptions;
    }

    /// <summary>
    /// Creates a new <typeparamref name="TContext"/> instance with Granit interceptors
    /// (audit, versioning, soft-delete) wired to the fakes.
    /// </summary>
    /// <returns>A configured <typeparamref name="TContext"/> instance.</returns>
    public TContext CreateContext()
    {
        AuditedEntityInterceptor auditInterceptor = new(_user, _clock, _guidGenerator, _tenant);
        VersioningInterceptor versioningInterceptor = new(_guidGenerator);
        SoftDeleteInterceptor softDeleteInterceptor = new(_user, _clock);

        DbContextOptionsBuilder<TContext> optionsBuilder = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(auditInterceptor, versioningInterceptor, softDeleteInterceptor);

        _configureOptions?.Invoke(optionsBuilder);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}
