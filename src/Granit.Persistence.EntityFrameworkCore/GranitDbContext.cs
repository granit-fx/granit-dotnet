using System.Linq.Expressions;
using System.Reflection;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Base class for every Granit DbContext that owns at least one <see cref="IMultiTenant"/>
/// entity. Centralises the dynamic multi-tenant query filter so that EF Core can parameterise
/// the current tenant value into the SQL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists.</b> EF Core only parameterises query filter values that are read
/// from a member of the DbContext instance. Values captured from any other source —
/// <see cref="Expression.Constant(object?)"/>, a closure-captured local, a property of a
/// constructor-injected service — are <i>inlined as literals</i> into the compiled SQL and
/// frozen for the lifetime of the cached model. The result is a cross-request data leak
/// (request A pins <c>WHERE TenantId = 'A'</c>, request B reuses the frozen plan and reads
/// A's rows). See the empirical repro in
/// <c>tests/Granit.Persistence.EntityFrameworkCore.Tests/MultiTenantFilterParameterizationReproTests.cs</c>.
/// </para>
/// <para>
/// <b>How it works.</b> <see cref="CurrentTenantId"/> and <see cref="IsMultiTenantFilterEnabled"/>
/// are instance properties on this DbContext. The filter expression registered by
/// <see cref="ConfigureMultiTenantFilter{TEntity}"/> reads them via the closed-over <c>this</c>
/// reference, which EF Core's relational query translator recognises and emits as a query
/// parameter (<c>@ef_filter__CurrentTenantId</c>) re-bound on every command.
/// </para>
/// <para>
/// <b>Compatibility.</b> The non-tenant filters (soft-delete, active, processing restriction,
/// publishable, merge tombstone) are still wired by
/// <see cref="ModelBuilderExtensions.ApplyGranitConventions"/> — those bypass values depend
/// on per-DbContext singletons and are unaffected by the per-request constant-folding issue.
/// Derived contexts should call <c>modelBuilder.ApplyGranitConventions(currentTenant: null, dataFilter)</c>
/// from their <see cref="OnGranitModelCreating"/> override (or let the default
/// <see cref="OnModelCreating"/> here do it).
/// </para>
/// </remarks>
public abstract class GranitDbContext : DbContext
{
    private static readonly MethodInfo ConfigureMultiTenantFilterMethod =
        typeof(GranitDbContext).GetMethod(
            nameof(ConfigureMultiTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!; // NOSONAR S3011 - intentional: ConfigureMultiTenantFilter must stay private to keep its `this`-binding (load-bearing for EF Core parameter extraction); reflection is the only way to invoke a generic instance method per-entity-type.

    /// <summary>
    /// The tenant context for this scope, captured at construction time.
    /// </summary>
    protected ICurrentTenant CurrentTenant { get; }

    /// <summary>
    /// Optional data filter used for service-level bypass of named filters.
    /// </summary>
    protected IDataFilter? DataFilter { get; }

    /// <summary>
    /// Current tenant identifier. Referenced verbatim inside the
    /// <see cref="IMultiTenant"/> filter expression — this is the SOLE source of the
    /// parameterised SQL value, so the property must remain a member of this DbContext.
    /// </summary>
    public virtual Guid? CurrentTenantId => CurrentTenant.IsAvailable ? CurrentTenant.Id : null;

    /// <summary>
    /// <c>true</c> when the <see cref="IMultiTenant"/> filter is active. Toggled via
    /// <see cref="IDataFilter.Disable{TFilter}"/> / <see cref="IDataFilter.Enable{TFilter}"/>.
    /// </summary>
    public virtual bool IsMultiTenantFilterEnabled
        => DataFilter?.IsEnabled<IMultiTenant>() ?? true;

    /// <summary>
    /// Builds a <see cref="GranitDbContext"/>. Derived classes forward their DI-injected
    /// services to this constructor.
    /// </summary>
    protected GranitDbContext(
        DbContextOptions options,
        ICurrentTenant currentTenant,
        IDataFilter? dataFilter = null)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentTenant);
        CurrentTenant = currentTenant;
        DataFilter = dataFilter;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sealed to enforce ordering: non-tenant conventions first (via
    /// <see cref="ModelBuilderExtensions.ApplyGranitConventions"/>), then the parameterised
    /// multi-tenant filter, then derived-class customisation via
    /// <see cref="OnGranitModelCreating"/>. Derived classes override the latter — not this.
    /// </remarks>
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1) Derived-class configuration first. Modules call `Ignore<>()` for
        //    entities they don't own, configure FKs, etc. — those must land
        //    before any iteration over `Model.GetEntityTypes()`.
        OnGranitModelCreating(modelBuilder);

        // 2) Non-tenant filters (soft-delete, active, processing restriction,
        //    publishable, merge tombstone). `currentTenant: null` skips the
        //    IMultiTenant block in this overload — this class owns that filter
        //    separately, with parameterised SQL.
        modelBuilder.ApplyGranitConventions(currentTenant: null, DataFilter);

        // 3) Parameterised IMultiTenant filter. Built inside a member of THIS
        //    DbContext so `CurrentTenantId` is recognised by EF Core's parameter
        //    extractor and emitted as @ef_filter__CurrentTenantId.
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
            .Where(et => typeof(IMultiTenant).IsAssignableFrom(et.ClrType)))
        {
            ConfigureMultiTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>
    /// Override point for derived classes. Configure your own <see cref="ModelBuilder"/>
    /// here (entity configurations, indexes, seed data) — the base class handles all Granit
    /// conventions on either side.
    /// </summary>
    protected virtual void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        // No-op by default.
    }

    /// <summary>
    /// Registers the parameterised <see cref="IMultiTenant"/> filter for
    /// <typeparamref name="TEntity"/>. Called via reflection from
    /// <see cref="OnModelCreating"/> so the lambda's <c>this</c> reference is bound to the
    /// current DbContext instance — EF Core relies on this to parameterise
    /// <see cref="CurrentTenantId"/>.
    /// </summary>
    private void ConfigureMultiTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        Expression<Func<TEntity, bool>> filter = e =>
            !IsMultiTenantFilterEnabled
            || EF.Property<Guid?>(e, nameof(IMultiTenant.TenantId)) == CurrentTenantId;

        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(GranitFilterNames.MultiTenant, filter);
    }
}
