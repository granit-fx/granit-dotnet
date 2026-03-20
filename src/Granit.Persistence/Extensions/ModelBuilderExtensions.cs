using System.Linq.Expressions;
using System.Reflection;
using Granit.Core.DataFiltering;
using Granit.Core.Domain;
using Granit.Core.MultiTenancy;
using Granit.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Granit.Persistence.Extensions;

/// <summary>
/// Extensions for configuring Granit conventions on the EF Core ModelBuilder.
/// </summary>
public static class ModelBuilderExtensions
{
    private static readonly MethodInfo SetEntityFilterMethod =
        typeof(ModelBuilderExtensions)
            .GetMethod(nameof(SetEntityFilter), BindingFlags.Static | BindingFlags.NonPublic)!; // NOSONAR S3011 - intentional: generic EF Core filter pattern requires reflection on private generic method

    private static readonly MethodInfo ConfigureConcurrencyStampMethod =
        typeof(ModelBuilderExtensions)
            .GetMethod(nameof(ConfigureConcurrencyStamp), BindingFlags.Static | BindingFlags.NonPublic)!; // NOSONAR S3011 - intentional: generic EF Core property configuration requires reflection

    /// <summary>
    /// Applies Granit conventions to all entity types in the model:
    /// <list type="bullet">
    ///   <item><b>Named query filters</b> (EF Core 10):
    ///     <see cref="ISoftDeletable"/> (<see cref="GranitFilterNames.SoftDelete"/>),
    ///     <see cref="IActive"/> (<see cref="GranitFilterNames.Active"/>),
    ///     <see cref="IProcessingRestrictable"/> (<see cref="GranitFilterNames.ProcessingRestrictable"/>),
    ///     <see cref="IMultiTenant"/> (<see cref="GranitFilterNames.MultiTenant"/>),
    ///     <see cref="IPublishable"/> (<see cref="GranitFilterNames.Publishable"/>).
    ///     Each interface registers its own independent named filter — bypass one without affecting others:
    ///     <c>query.IgnoreQueryFilters([GranitFilterNames.SoftDelete])</c>.
    ///   </item>
    ///   <item><b>Translation conventions</b>:
    ///     <see cref="ITranslation{TParent}"/> → FK, cascade delete, unique index (ParentId, Culture).
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="modelBuilder">The EF Core ModelBuilder.</param>
    /// <param name="currentTenant">
    /// Current tenant service. If <c>null</c>, the multi-tenant filter is not applied.
    /// Pass the instance injected in the DbContext constructor for correct lazy evaluation
    /// (re-evaluated on each query via AsyncLocal).
    /// </param>
    /// <param name="dataFilter">
    /// Data filter service for service-level bypass (all queries in the current async flow).
    /// If <c>null</c>, all filters are always applied.
    /// When provided, each filter can be individually bypassed via <c>IDataFilter.Disable&lt;TFilter&gt;()</c>.
    /// For single-query bypass, use <c>query.IgnoreQueryFilters([GranitFilterNames.SoftDelete])</c> instead.
    /// </param>
    public static ModelBuilder ApplyGranitConventions(
        this ModelBuilder modelBuilder,
        ICurrentTenant? currentTenant = null,
        IDataFilter? dataFilter = null)
    {
        // FilterProxy wraps IDataFilter? and exposes simple boolean properties.
        // EF Core extracts property access on a ConstantExpression as a query parameter
        // re-evaluated on each query — the same mechanism used by currentTenant.Id in
        // the multi-tenant filter. This avoids the risk of EF Core attempting SQL
        // translation of a generic method call (IsEnabled<T>()).
        FilterProxy proxy = new(dataFilter);

        foreach (Type clrType in modelBuilder.Model.GetEntityTypes().Select(entityType => entityType.ClrType))
        {
            bool hasSoftDelete = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            bool hasActive = typeof(IActive).IsAssignableFrom(clrType);
            bool hasProcessingRestriction = typeof(IProcessingRestrictable).IsAssignableFrom(clrType);
            bool hasMultiTenant = typeof(IMultiTenant).IsAssignableFrom(clrType)
                && currentTenant is not null;
            bool hasPublishable = typeof(IPublishable).IsAssignableFrom(clrType);

            if (!hasSoftDelete && !hasActive && !hasProcessingRestriction && !hasMultiTenant && !hasPublishable)
            {
                continue;
            }

            SetEntityFilterMethod // NOSONAR S3011 - intentional: generic EF Core filter pattern requires reflection
                .MakeGenericMethod(clrType)
                .Invoke(null, [modelBuilder, currentTenant, proxy]);
        }

        // --- Concurrency token conventions ---
        // Detects IConcurrencyAware implementations and configures:
        //   - ConcurrencyStamp as a concurrency token (IsConcurrencyToken)
        //   - VARCHAR(36) max length
        foreach (Type clrType in modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(IConcurrencyAware).IsAssignableFrom(clrType)))
        {
            ConfigureConcurrencyStampMethod // NOSONAR S3011 - intentional: generic EF Core property configuration requires reflection
                .MakeGenericMethod(clrType)
                .Invoke(null, [modelBuilder]);
        }

        // --- Translation conventions ---
        // Detects ITranslation<TParent> implementations and configures:
        //   - FK from Translation.ParentId → Parent.Id with cascade delete
        //   - Unique index on (ParentId, Culture)
        //   - Culture max length (20, BCP 47)
        foreach (Type clrType in modelBuilder.Model.GetEntityTypes().ToList().Select(entityType => entityType.ClrType)) // NOSONAR S3445 - ToList() required: ConfigureTranslation modifies the model (adds FK, index), enumerating a live collection would throw InvalidOperationException
        {
            Type? translationInterface = clrType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(ITranslation<>));

            if (translationInterface is null)
            {
                continue;
            }

            Type parentType = translationInterface.GetGenericArguments()[0];

            typeof(ModelBuilderExtensions)
                .GetMethod(nameof(ConfigureTranslation), BindingFlags.Static | BindingFlags.NonPublic)! // NOSONAR S3011 - intentional: generic EF Core convention pattern requires reflection
                .MakeGenericMethod(clrType, parentType)
                .Invoke(null, [modelBuilder]);
        }

        // --- SingleValueObject<T> conventions ---
        // Auto-applies value converters for properties whose CLR type inherits from
        // SingleValueObject<T>, mapping them to the underlying primitive column type.
        ApplySingleValueObjectConverters(modelBuilder);

        return modelBuilder;
    }

    // Scans all entity properties for SingleValueObject<T> types and applies a ValueConverter
    // that extracts/wraps the underlying primitive. No schema change — same column type.
    private static void ApplySingleValueObjectConverters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                Type? svoBase = GetSingleValueObjectBase(property.ClrType);
                if (svoBase is null)
                {
                    continue;
                }

                Type primitiveType = svoBase.GetGenericArguments()[0];
                Type converterType = typeof(SingleValueObjectConverter<,>)
                    .MakeGenericType(property.ClrType, primitiveType);

                property.SetValueConverter(
                    (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)
                    Activator.CreateInstance(converterType)!);
            }
        }
    }

    private static Type? GetSingleValueObjectBase(Type type)
    {
        Type svoOpenType = typeof(SingleValueObject<>);
        Type? current = type;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == svoOpenType)
            {
                return current;
            }

            current = current.BaseType;
        }

        return null;
    }

    // Configures a translation entity type: FK, cascade delete, unique index, Culture max length.
    private static void ConfigureTranslation<TTranslation, TParent>(ModelBuilder modelBuilder)
        where TTranslation : class, ITranslation<TParent>
        where TParent : Entity
    {
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TTranslation> builder =
            modelBuilder.Entity<TTranslation>();

        // Culture column: max 20 chars (BCP 47 — same as LocalizationOverride)
        builder.Property(t => t.Culture)
            .HasMaxLength(20)
            .IsRequired();

        // FK: Translation.ParentId → Parent.Id, cascade delete (RGPD/ISO 27001 compliance)
        builder.HasOne(t => t.Parent)
            .WithMany()
            .HasForeignKey(t => t.ParentId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);

        // Unique index: one translation per (parent, culture)
        builder.HasIndex(t => new { t.ParentId, t.Culture })
            .IsUnique();
    }

    // Registers one named HasQueryFilter per applicable filter interface for TEntity (EF Core 10).
    // Each filter is independent: bypass one per query via IgnoreQueryFilters([GranitFilterNames.X])
    // or bypass all queries in the current async flow via IDataFilter.Disable<TFilter>().
    //
    // Pattern per filter:
    //   bypass = !proxy.XEnabled  (re-evaluated as a query parameter via ConstantExpression)
    //   real   = <interface condition>
    //   filter = bypass || real
    private static void SetEntityFilter<TEntity>(
        ModelBuilder modelBuilder,
        ICurrentTenant? currentTenant,
        FilterProxy proxy)
        where TEntity : class
    {
        ParameterExpression param = Expression.Parameter(typeof(TEntity), "e");
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder = modelBuilder.Entity<TEntity>();

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.SoftDeleteEnabled)));
            Expression notDeleted = Expression.Not(
                Expression.Property(param, nameof(ISoftDeletable.IsDeleted)));
            builder.HasQueryFilter(GranitFilterNames.SoftDelete,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, notDeleted), param));
        }

        if (typeof(IActive).IsAssignableFrom(typeof(TEntity)))
        {
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.ActiveEnabled)));
            Expression isActive = Expression.Property(param, nameof(IActive.IsActive));
            builder.HasQueryFilter(GranitFilterNames.Active,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, isActive), param));
        }

        if (typeof(IProcessingRestrictable).IsAssignableFrom(typeof(TEntity)))
        {
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.ProcessingRestrictableEnabled)));
            Expression notRestricted = Expression.Not(
                Expression.Property(param, nameof(IProcessingRestrictable.IsProcessingRestricted)));
            builder.HasQueryFilter(GranitFilterNames.ProcessingRestrictable,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, notRestricted), param));
        }

        if (typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity)) && currentTenant is not null)
        {
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.MultiTenantEnabled)));
            Expression tenantMatch = Expression.Equal(
                Expression.Property(param, nameof(IMultiTenant.TenantId)),
                Expression.Property(Expression.Constant(currentTenant), nameof(ICurrentTenant.Id)));
            builder.HasQueryFilter(GranitFilterNames.MultiTenant,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, tenantMatch), param));
        }

        if (typeof(IPublishable).IsAssignableFrom(typeof(TEntity)))
        {
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.PublishableEnabled)));
            Expression isPublished = Expression.Property(param, nameof(IPublishable.IsPublished));
            builder.HasQueryFilter(GranitFilterNames.Publishable,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, isPublished), param));
        }
    }

    // Configures the ConcurrencyStamp property as a concurrency token with VARCHAR(36).
    private static void ConfigureConcurrencyStamp<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IConcurrencyAware
    {
        modelBuilder.Entity<TEntity>()
            .Property(e => e.ConcurrencyStamp)
            .HasMaxLength(36)
            .IsConcurrencyToken();
    }

    // Internal wrapper: EF Core evaluates simple property access on a ConstantExpression
    // as a query parameter re-evaluated on each query execution.
    // Registered as Singleton + static AsyncLocal in DataFilter → the captured instance
    // reads the correct per-flow state on every query, regardless of model caching.
    private sealed class FilterProxy(IDataFilter? dataFilter)
    {
        private readonly IDataFilter? _dataFilter = dataFilter;

        public bool SoftDeleteEnabled => _dataFilter?.IsEnabled<ISoftDeletable>() ?? true;
        public bool ActiveEnabled => _dataFilter?.IsEnabled<IActive>() ?? true;
        public bool ProcessingRestrictableEnabled => _dataFilter?.IsEnabled<IProcessingRestrictable>() ?? true;
        public bool MultiTenantEnabled => _dataFilter?.IsEnabled<IMultiTenant>() ?? true;
        public bool PublishableEnabled => _dataFilter?.IsEnabled<IPublishable>() ?? true;
    }
}
