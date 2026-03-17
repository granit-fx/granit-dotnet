using Granit.Persistence.Interceptors;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.EntityFrameworkCore;

/// <summary>
/// Creates EF Core <typeparamref name="TContext"/> instances backed by
/// an in-memory SQLite database with Granit interceptors wired automatically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recommended for most tests.</b> SQLite enforces foreign key constraints,
/// supports transactions, and translates LINQ to real SQL — catching issues
/// that the EF Core In-Memory provider silently ignores.
/// </para>
/// <para>
/// The SQLite connection is opened once and kept alive for the factory's lifetime.
/// All contexts created by <see cref="CreateContext"/> share the same in-memory
/// database. Call <see cref="Dispose"/> to close the connection and release
/// the database.
/// </para>
/// </remarks>
/// <typeparam name="TContext">
/// The <see cref="DbContext"/> type. Must have a constructor accepting
/// <see cref="DbContextOptions{TContext}"/>.
/// </typeparam>
public sealed class SqliteDbContextFactory<TContext> : IDisposable
    where TContext : DbContext
{
    private readonly SqliteConnection _connection;
    private readonly FakeCurrentTenant _tenant;
    private readonly FakeCurrentUser _user;
    private readonly FakeClock _clock;
    private readonly FakeGuidGenerator _guidGenerator;
    private readonly Action<DbContextOptionsBuilder>? _configureOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDbContextFactory{TContext}"/> class.
    /// Opens an in-memory SQLite connection that persists for the factory's lifetime.
    /// </summary>
    /// <param name="tenant">Fake tenant (defaults to a new <see cref="FakeCurrentTenant"/>).</param>
    /// <param name="user">Fake user (defaults to a new <see cref="FakeCurrentUser"/>).</param>
    /// <param name="clock">Fake clock (defaults to a new <see cref="FakeClock"/>).</param>
    /// <param name="guidGenerator">Fake GUID generator (defaults to a new <see cref="FakeGuidGenerator"/>).</param>
    /// <param name="configureOptions">Optional callback for additional <see cref="DbContextOptionsBuilder"/> configuration.</param>
    public SqliteDbContextFactory(
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
        _configureOptions = configureOptions;

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    /// <summary>
    /// Creates a new <typeparamref name="TContext"/> instance with Granit interceptors
    /// (audit, versioning, soft-delete) wired to the fakes.
    /// </summary>
    /// <param name="ensureCreated">
    /// When <c>true</c> (default), calls <see cref="DatabaseFacade.EnsureCreated"/>
    /// to create the schema. Set to <c>false</c> if the schema is already created
    /// or you want to apply migrations manually.
    /// </param>
    /// <returns>A configured <typeparamref name="TContext"/> instance.</returns>
    public TContext CreateContext(bool ensureCreated = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        AuditedEntityInterceptor auditInterceptor = new(_user, _clock, _guidGenerator, _tenant);
        VersioningInterceptor versioningInterceptor = new(_guidGenerator);
        SoftDeleteInterceptor softDeleteInterceptor = new(_user, _clock);

        DbContextOptionsBuilder<TContext> optionsBuilder = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .AddInterceptors(auditInterceptor, versioningInterceptor, softDeleteInterceptor);

        _configureOptions?.Invoke(optionsBuilder);

        var context = (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;

        if (ensureCreated)
        {
            context.Database.EnsureCreated();
        }

        return context;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _connection.Dispose();
        }
    }
}
