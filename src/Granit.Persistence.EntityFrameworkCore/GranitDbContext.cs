using System.Linq.Expressions;
using System.Reflection;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Conventions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

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
/// <b>All named filters are context-bound.</b> The convention filters (soft-delete, active,
/// processing restriction, publishable, merge tombstone) and the translation mirrors are
/// registered here with bypass flags read via <c>this</c> — NOT by
/// <see cref="ModelBuilderExtensions.ApplyGranitConventions"/>, whose proxy-captured flags
/// are constant-folded into the cached query plan and made
/// <c>IDataFilter.Disable&lt;T&gt;()</c> a silent no-op on relational providers (#3174).
/// The base class calls <c>ApplyGranitConventions</c> with the filter pass disabled;
/// derived contexts override <see cref="OnGranitModelCreating"/> and never call it themselves.
/// </para>
/// </remarks>
public abstract class GranitDbContext : DbContext
{
    private static readonly MethodInfo ConfigureMultiTenantFilterMethod =
        typeof(GranitDbContext).GetMethod(
            nameof(ConfigureMultiTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!; // NOSONAR S3011 - intentional: ConfigureMultiTenantFilter must stay private to keep its `this`-binding (load-bearing for EF Core parameter extraction); reflection is the only way to invoke a generic instance method per-entity-type.

    private static readonly MethodInfo ConfigureConventionFiltersMethod =
        typeof(GranitDbContext).GetMethod(
            nameof(ConfigureConventionFilters),
            BindingFlags.Instance | BindingFlags.NonPublic)!; // NOSONAR S3011 - same `this`-binding requirement as ConfigureMultiTenantFilter (#3174).

    private static readonly MethodInfo ConfigureTranslationFiltersMethod =
        typeof(GranitDbContext).GetMethod(
            nameof(ConfigureTranslationFilters),
            BindingFlags.Instance | BindingFlags.NonPublic)!; // NOSONAR S3011 - same `this`-binding requirement as ConfigureMultiTenantFilter (#3174).

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

    // The five convention-filter bypass flags below are instance members for the same
    // load-bearing reason as IsMultiTenantFilterEnabled: EF Core only re-evaluates filter
    // values per query (@ef_filter__* parameters) when they are read from a member of the
    // current DbContext. Reading them from any other captured object gets constant-folded
    // into the cached query plan — which made IDataFilter.Disable<T>() a silent no-op on
    // relational providers for every FilterProxy-backed filter (#3174).

    /// <summary><c>true</c> when the <see cref="ISoftDeletable"/> filter is active.</summary>
    public virtual bool IsSoftDeleteFilterEnabled
        => DataFilter?.IsEnabled<ISoftDeletable>() ?? true;

    /// <summary><c>true</c> when the <see cref="IActive"/> filter is active.</summary>
    public virtual bool IsActiveFilterEnabled
        => DataFilter?.IsEnabled<IActive>() ?? true;

    /// <summary><c>true</c> when the <see cref="IProcessingRestrictable"/> filter is active.</summary>
    public virtual bool IsProcessingRestrictionFilterEnabled
        => DataFilter?.IsEnabled<IProcessingRestrictable>() ?? true;

    /// <summary><c>true</c> when the <see cref="IPublishable"/> filter is active.</summary>
    public virtual bool IsPublishableFilterEnabled
        => DataFilter?.IsEnabled<IPublishable>() ?? true;

    /// <summary><c>true</c> when the <see cref="IHasMergeTombstone"/> filter is active.</summary>
    public virtual bool IsMergeTombstoneFilterEnabled
        => DataFilter?.IsEnabled<IHasMergeTombstone>() ?? true;

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
    /// Sealed to enforce ordering: derived configuration first, then the parameterised
    /// multi-tenant filter, then <see cref="ModelBuilderExtensions.ApplyGranitConventions"/>
    /// last. The conventions pass finishes by removing any auto-discovered
    /// <see cref="SingleValueObject{T}"/> entity types and applying their value
    /// converters — that cleanup must run AFTER every <c>Entity&lt;T&gt;()</c> call that
    /// can re-fire EF Core's navigation discovery (notably the multi-tenant filter
    /// loop below). Derived classes override <see cref="OnGranitModelCreating"/>, not this.
    /// </remarks>
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1) Derived-class configuration first. Modules call `Ignore<>()` for
        //    entities they don't own, configure FKs, etc. — those must land
        //    before any iteration over `Model.GetEntityTypes()`.
        OnGranitModelCreating(modelBuilder);

        // 2) Parameterised IMultiTenant filter. Built inside a member of THIS
        //    DbContext so `CurrentTenantId` is recognised by EF Core's parameter
        //    extractor and emitted as @ef_filter__CurrentTenantId. Calling
        //    `Entity<TEntity>().HasQueryFilter(...)` here re-fires navigation
        //    discovery for TEntity, which can re-add `SingleValueObject<T>`-typed
        //    CLR properties as phantom navigation entities — step 3 cleans that up.
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
            .Where(et => typeof(IMultiTenant).IsAssignableFrom(et.ClrType))
            .ToList())
        {
            ConfigureMultiTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }

        // 2-bis) Convention filters (soft-delete, active, processing restriction,
        //    publishable, merge tombstone) + translation mirrors, built inside members
        //    of THIS DbContext so the bypass flags are re-bound per query
        //    (@ef_filter__*). The proxy-based registration in ApplyGranitConventions
        //    is constant-folded into the cached plan and made IDataFilter.Disable<T>()
        //    a silent no-op on relational providers (#3174) — step 3 skips it.
        //    Runs BEFORE step 3 because Entity<TEntity>() re-fires navigation
        //    discovery, and the SVO cleanup at the end of step 3 must stay the final
        //    pass over the model surface.
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
            .Where(et => ConventionFilterInterfaces.Any(i => i.IsAssignableFrom(et.ClrType)))
            .ToList())
        {
            ConfigureConventionFiltersMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            Type? translationInterface = entityType.ClrType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(ITranslation<>));

            if (translationInterface is not null)
            {
                ConfigureTranslationFiltersMethod
                    .MakeGenericMethod(entityType.ClrType, translationInterface.GetGenericArguments()[0])
                    .Invoke(this, [modelBuilder]);
            }
        }

        // 3) Non-filter conventions. `currentTenant: null` skips the IMultiTenant block;
        //    `registerConventionFilters: false` skips the proxy-based named filters — this
        //    class owns ALL filters, with parameterised bypass flags (steps 2 / 2-bis).
        //    Runs LAST so the SVO cleanup is the final pass over the model surface.
        //    Native-conventions mode (#3158, test-gated): the scalar passes run as
        //    IModelFinalizingConventions (see OnGranitConfigureConventionsCore) and only
        //    the value-object trio still executes here.
        if (GranitNativeConventions.Enabled)
        {
            modelBuilder.ApplyGranitValueObjectPasses();
        }
        else
        {
            modelBuilder.ApplyGranitConventionsCore(currentTenant: null, DataFilter, registerConventionFilters: false);
        }

        // 4) External, opt-in model extensions registered in DI by separate packages — applied LAST so they
        //    get the final say (after conventions). Lets e.g. a PostGIS package add a generated geography
        //    column to a module's address table without the module depending on NetTopologySuite. Each
        //    extension self-guards on provider + target entity type (it sees every context's model).
        ApplyModelExtensions(modelBuilder);
    }

    /// <summary>
    /// Resolves and applies the registered <see cref="IGranitModelExtension"/> set from the application
    /// service provider. No-op when none are registered (the common case) or the provider is unavailable.
    /// </summary>
    /// <remarks>
    /// The cross-configuration bleed protection for the augmented model lives elsewhere:
    /// <see cref="DbContextOptionsBuilderExtensions.UseGranitInterceptors"/> installs
    /// <see cref="GranitModelCacheKeyFactory"/>, which folds the extension set into the model cache key. A
    /// context that augments its model but does NOT go through <c>UseGranitInterceptors</c> would lose that
    /// safety net — keep the two wired together.
    /// </remarks>
    private void ApplyModelExtensions(ModelBuilder modelBuilder)
    {
        IServiceProvider? applicationServices = this.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;

        if (applicationServices is null)
        {
            return;
        }

        foreach (IGranitModelExtension extension in applicationServices.GetServices<IGranitModelExtension>())
        {
            extension.Apply(modelBuilder, this);
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

    /// <inheritdoc />
    /// <remarks>
    /// Sealed for the same ordering reasons as <see cref="OnModelCreating"/>. In
    /// native-conventions mode (#3158, test-gated) the scalar Granit passes (enum-as-string,
    /// concurrency stamp, merge tombstone, ownership indexes, translation config) run as
    /// <c>IModelFinalizingConvention</c>s. Derived contexts extend via
    /// <see cref="OnGranitConfigureConventions"/>.
    /// </remarks>
    protected sealed override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        if (GranitNativeConventions.Enabled)
        {
            configurationBuilder.Conventions.Add(_ => new GranitEnumStringConvention());
            configurationBuilder.Conventions.Add(_ => new GranitConcurrencyStampConvention());
            configurationBuilder.Conventions.Add(_ => new GranitMergeTombstoneConvention());
            configurationBuilder.Conventions.Add(_ => new GranitOwnershipIndexConvention());
            configurationBuilder.Conventions.Add(_ => new GranitTranslationConvention());
        }

        OnGranitConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Override point for derived classes that need additional model-configuration
    /// conventions; the Granit set is always registered first.
    /// </summary>
    protected virtual void OnGranitConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // No-op by default.
    }

    /// <summary>
    /// When <c>true</c>, a row whose <see cref="IMultiTenant.TenantId"/> is <see langword="null"/> is
    /// treated as global and stays visible under every tenant scope (and to anonymous requests), in
    /// addition to the current tenant's own rows. Defaults to <c>false</c> — strict isolation, where a
    /// tenant sees only its own rows. Override to <c>true</c> in a context whose null-tenant rows are
    /// deliberately shared (e.g. OpenIddict's global clients and scopes).
    /// </summary>
    protected virtual bool TenantFilterTreatsNullAsGlobal => false;

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
        // The disjunction is chosen at model-creation time (per derived context type). Both branches
        // read CurrentTenantId via `this`, so EF Core still parameterises it (@ef_filter__CurrentTenantId).
        Expression<Func<TEntity, bool>> filter = TenantFilterTreatsNullAsGlobal
            ? e => !IsMultiTenantFilterEnabled
                || EF.Property<Guid?>(e, nameof(IMultiTenant.TenantId)) == null
                || EF.Property<Guid?>(e, nameof(IMultiTenant.TenantId)) == CurrentTenantId
            : e => !IsMultiTenantFilterEnabled
                || EF.Property<Guid?>(e, nameof(IMultiTenant.TenantId)) == CurrentTenantId;

        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(GranitFilterNames.MultiTenant, filter);
    }

    private static readonly Type[] ConventionFilterInterfaces =
    [
        typeof(ISoftDeletable),
        typeof(IActive),
        typeof(IProcessingRestrictable),
        typeof(IPublishable),
        typeof(IHasMergeTombstone),
    ];

    /// <summary>
    /// Registers the named convention filters for <typeparamref name="TEntity"/> with
    /// bypass flags read via <c>this</c> — the sole shape EF Core re-binds per query
    /// (<c>@ef_filter__*</c>). Same registration names as the legacy proxy path, so
    /// per-query bypass (<c>IgnoreQueryFilters([GranitFilterNames.X])</c>) is unchanged.
    /// </summary>
    private void ConfigureConventionFilters<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder =
            modelBuilder.Entity<TEntity>();

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(GranitFilterNames.SoftDelete,
                e => !IsSoftDeleteFilterEnabled
                    || !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)));
        }

        if (typeof(IActive).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(GranitFilterNames.Active,
                e => !IsActiveFilterEnabled
                    || EF.Property<bool>(e, nameof(IActive.Activated)));
        }

        if (typeof(IProcessingRestrictable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(GranitFilterNames.ProcessingRestrictable,
                e => !IsProcessingRestrictionFilterEnabled
                    || !EF.Property<bool>(e, nameof(IProcessingRestrictable.IsProcessingRestricted)));
        }

        if (typeof(IPublishable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(GranitFilterNames.Publishable,
                e => !IsPublishableFilterEnabled
                    || EF.Property<bool>(e, nameof(IPublishable.IsPublished)));
        }

        if (typeof(IHasMergeTombstone).IsAssignableFrom(typeof(TEntity)))
        {
            builder.HasQueryFilter(GranitFilterNames.MergeTombstone,
                e => !IsMergeTombstoneFilterEnabled
                    || EF.Property<Guid?>(e, nameof(IHasMergeTombstone.MergedIntoId)) == null);
        }
    }

    /// <summary>
    /// Mirrors the parent's soft-delete / active filters onto a translation entity (EF Core
    /// filter-consistency requirement on required principals), with <c>this</c>-bound bypass
    /// flags — same shapes as the legacy proxy mirrors in <c>ConfigureTranslation</c>.
    /// </summary>
    private void ConfigureTranslationFilters<TTranslation, TParent>(ModelBuilder modelBuilder)
        where TTranslation : class, ITranslation<TParent>
        where TParent : Entity
    {
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TTranslation> builder =
            modelBuilder.Entity<TTranslation>();

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TParent)))
        {
            builder.HasQueryFilter(GranitFilterNames.SoftDelete,
                t => !IsSoftDeleteFilterEnabled
                    || t.Parent == null
                    || !EF.Property<bool>(t.Parent, nameof(ISoftDeletable.IsDeleted)));
        }

        if (typeof(IActive).IsAssignableFrom(typeof(TParent)))
        {
            builder.HasQueryFilter(GranitFilterNames.Active,
                t => !IsActiveFilterEnabled
                    || t.Parent == null
                    || EF.Property<bool>(t.Parent, nameof(IActive.Activated)));
        }
    }
}
