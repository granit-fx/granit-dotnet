using System.Linq.Expressions;
using System.Reflection;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

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

    private static readonly MethodInfo ConfigureMergeTombstoneMethod =
        typeof(ModelBuilderExtensions)
            .GetMethod(nameof(ConfigureMergeTombstone), BindingFlags.Static | BindingFlags.NonPublic)!; // NOSONAR S3011 - intentional: generic EF Core property configuration requires reflection

    /// <summary>
    /// Applies Granit conventions to all entity types in the model:
    /// <list type="bullet">
    ///   <item><b>Named query filters</b> (EF Core 10):
    ///     <see cref="ISoftDeletable"/> (<see cref="GranitFilterNames.SoftDelete"/>),
    ///     <see cref="IActive"/> (<see cref="GranitFilterNames.Active"/>),
    ///     <see cref="IProcessingRestrictable"/> (<see cref="GranitFilterNames.ProcessingRestrictable"/>),
    ///     <see cref="IMultiTenant"/> (<see cref="GranitFilterNames.MultiTenant"/>),
    ///     <see cref="IPublishable"/> (<see cref="GranitFilterNames.Publishable"/>),
    ///     <see cref="IHasMergeTombstone"/> (<see cref="GranitFilterNames.MergeTombstone"/>).
    ///     Each interface registers its own independent named filter — bypass one without affecting others:
    ///     <c>query.IgnoreQueryFilters([GranitFilterNames.SoftDelete])</c>.
    ///   </item>
    ///   <item><b>Merge tombstone column convention</b>:
    ///     Implementors of <see cref="IHasMergeTombstone"/> get the <c>MergedIntoId</c> +
    ///     <c>MergedAt</c> columns and an index on <c>MergedIntoId</c> auto-applied.
    ///   </item>
    ///   <item><b>Ownership index convention</b>:
    ///     Implementors of <see cref="IOwnable"/> get an index on <c>(TenantId, OwnerId)</c>
    ///     when also <see cref="IMultiTenant"/>, or on <c>(OwnerId)</c> alone otherwise.
    ///     Aligned with the multi-tenant query filter for efficient "my entities" lookups.
    ///   </item>
    ///   <item><b>Translation conventions</b>:
    ///     <see cref="ITranslation{TParent}"/> → FK, cascade delete, unique index (ParentId, Culture).
    ///   </item>
    ///   <item><b>Enum persistence convention</b>:
    ///     Every enum property is persisted as its PascalCase string name in a
    ///     <c>varchar</c> column sized to the longest value (min 20). Opt out per
    ///     property with <see cref="PersistAsIntAttribute"/>; <see cref="FlagsAttribute"/>
    ///     enums are skipped automatically. Explicit
    ///     <c>HasConversion&lt;...&gt;()</c> calls in entity configurations always win.
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
    /// <example>
    /// At runtime, both services come from DI. From an
    /// <see cref="Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory{TContext}"/>
    /// — where there is no container — pass the framework-provided stubs so that the model
    /// snapshot encodes the same named query filters as the runtime model and EF Core 10 does
    /// not raise <c>PendingModelChangesWarning</c>:
    /// <code>
    /// return new MyDbContext(
    ///     options,
    ///     GranitDesignTime.CurrentTenant,
    ///     GranitDesignTime.DataFilter);
    /// </code>
    /// </example>
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
            bool hasMergeTombstone = typeof(IHasMergeTombstone).IsAssignableFrom(clrType);

            if (!hasSoftDelete && !hasActive && !hasProcessingRestriction && !hasMultiTenant && !hasPublishable && !hasMergeTombstone)
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

        // --- Merge tombstone column conventions ---
        // Detects IHasMergeTombstone implementors and adds:
        //   - MergedIntoId column (Guid?) + index (lookup "who merged into X" + tombstone listing)
        //   - MergedAt column (DateTimeOffset?)
        // The matching query filter (excludes tombstoned rows from standard queries) is
        // registered earlier alongside SoftDelete / IMultiTenant / etc.
        foreach (Type clrType in modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(IHasMergeTombstone).IsAssignableFrom(clrType)))
        {
            ConfigureMergeTombstoneMethod // NOSONAR S3011 - intentional: generic EF Core property configuration requires reflection
                .MakeGenericMethod(clrType)
                .Invoke(null, [modelBuilder]);
        }

        // --- Ownership index convention ---
        // Detects IOwnable implementors and adds an index on (TenantId, OwnerId) when also
        // IMultiTenant, or on (OwnerId) alone otherwise. Aligned with the multi-tenant
        // query filter so that "my entities in this tenant" lookups remain a seek.
        // No query filter is registered — ownership is an exposed column (admin must be
        // able to list "all entities owned by Alice"), not a hidden filter.
        ApplyOwnershipIndexes(modelBuilder);

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
        // 1. Remove any SingleValueObject<T> types that EF Core auto-discovered as entity
        //    types. These are value objects (e.g. PlanId, InvoiceId) used as scalar
        //    properties on entities — they must NOT be treated as entities themselves.
        RemoveSingleValueObjectEntityTypes(modelBuilder);

        // 2. Auto-apply value converters for properties whose CLR type inherits from
        //    SingleValueObject<T>, mapping them to the underlying primitive column type.
        ApplySingleValueObjectConverters(modelBuilder);

        // --- Enum persistence convention ---
        // Persist enum properties as their PascalCase string name (varchar) by default.
        // Opt out per property with [PersistAsInt]; [Flags] enums are skipped automatically.
        ApplyEnumStringConverters(modelBuilder);

        return modelBuilder;
    }

    // Adds an automatic composite index on (TenantId, OwnerId) for IOwnable + IMultiTenant
    // entities, or on (OwnerId) alone otherwise. Uses the Fluent API
    // modelBuilder.Entity(...).HasIndex(...) rather than the low-level
    // entityType.AddIndex(...) to stay aligned with ModelBuilder validations.
    //
    // Skips entities without their own table (TPH derived types, keyless query types).
    // De-duplicates: if an index over the same property set already exists, no new
    // index is added — module authors keep the freedom to declare it explicitly with
    // a custom name when they want one.
    private static void ApplyOwnershipIndexes(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IOwnable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            string? tableName = entityType.GetTableName();
            if (tableName is null)
            {
                // Keyless / TPH-derived / view-mapped — no own table to index.
                continue;
            }

            bool isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType);
            string[] propertyNames = isMultiTenant
                ? [nameof(IMultiTenant.TenantId), nameof(IOwnable.OwnerId)]
                : [nameof(IOwnable.OwnerId)];

            // De-dup: skip if an index over the exact same property set already exists.
            bool alreadyIndexed = entityType.GetIndexes().Any(idx =>
                idx.Properties.Count == propertyNames.Length
                && idx.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

            if (alreadyIndexed)
            {
                continue;
            }

            string suffix = isMultiTenant ? "tenant_owner" : "owner";
            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(propertyNames)
                .HasDatabaseName($"ix_{tableName}_{suffix}");
        }
    }

    // Auto-applies EnumToStringConverter<TEnum> to every enum property in the model,
    // unless an explicit converter is already configured, the property is marked
    // [PersistAsInt], or the enum carries [Flags] (bitmask semantics).
    //
    // Rationale: persisting enums as their string name (e.g. "Pending") instead of
    // the underlying ordinal (e.g. 0) protects against silent breakage when enum
    // values are reordered, and keeps the database lisible for ops/SQL audits. The
    // wire format already uses string names via JsonStringEnumConverter — this
    // convention restores symmetry between the HTTP surface and the DB columns.
    private static void ApplyEnumStringConverters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                Type? enumType = GetEnumType(property.ClrType);
                if (enumType is null)
                {
                    continue;
                }

                // Respect explicit overrides: a HasConversion<...>() call in the entity
                // configuration always wins over the convention. EF Core exposes the
                // override in two different ways depending on the overload:
                //   - HasConversion(ValueConverter) / HasConversion<TConverter>()
                //       → SetValueConverter, surfaced by GetValueConverter()
                //   - HasConversion<TProvider>()  (e.g. HasConversion<short>())
                //       → SetProviderClrType only; the ValueConverter is materialised
                //         later from the type mapping, so GetValueConverter() is null
                //         at convention-application time. Without the second check the
                //         convention stacks EnumToStringConverter on top of the
                //         provider type and EF crashes on default-value sanitisation
                //         (FormatException trying to parse "Available" as short).
                if (property.GetValueConverter() is not null
                    || property.GetProviderClrType() is not null)
                {
                    continue;
                }

                // Skip opt-out: property marked [PersistAsInt] with a documented reason.
                if (property.PropertyInfo?.GetCustomAttribute<PersistAsIntAttribute>() is not null)
                {
                    continue;
                }

                // [Flags] enums encode multiple values bitwise — storing them as a single
                // string name would lose information. Keep the int column unless an
                // explicit converter was configured above.
                if (enumType.GetCustomAttribute<FlagsAttribute>() is not null)
                {
                    continue;
                }

                Type converterType = typeof(EnumToStringConverter<>).MakeGenericType(enumType);
                property.SetValueConverter(
                    (ValueConverter)Activator.CreateInstance(converterType)!);

                // Preserve any explicit HasMaxLength(N) the entity configuration already set
                // (e.g. AuditEntry.Category keeps its 50-char column even though the convention
                // floor would compute a smaller value). Only apply the default when missing.
                if (property.GetMaxLength() is null)
                {
                    int longestName = Enum.GetNames(enumType).Max(name => name.Length);
                    property.SetMaxLength(Math.Max(20, longestName + 4));
                }
            }
        }
    }

    // Returns the enum CLR type for an enum or Nullable<TEnum> property, or null otherwise.
    private static Type? GetEnumType(Type clrType)
    {
        Type underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return underlying.IsEnum ? underlying : null;
    }

    // Removes any SingleValueObject<T> subclass that EF Core auto-discovered as an entity type.
    // When a class property (e.g. Subscription.PlanId of type PlanId : SingleValueObject<Guid>)
    // is not explicitly configured via builder.Property(), EF Core's convention scanner treats
    // the CLR type as a navigation target and adds it as an entity type — which then fails
    // validation because no primary key is defined. This step removes those phantom entities
    // so the subsequent converter step can safely map the property as a scalar column.
    //
    // Convention also detaches any auto-discovered foreign key whose principal is an SVO type
    // (e.g. Party.AvatarTempId : BlobReference creates an auto-FK that EF refuses to release
    // when we call RemoveEntityType). The underlying scalar column survives and is then wrapped
    // by ApplySingleValueObjectConverters with the matching ValueConverter.
    private static void RemoveSingleValueObjectEntityTypes(ModelBuilder modelBuilder)
    {
        var svoEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(et => GetSingleValueObjectBase(et.ClrType) is not null)
            .ToList();

        if (svoEntityTypes.Count == 0)
        {
            return;
        }

        HashSet<Type> svoClrTypes = [.. svoEntityTypes.Select(et => et.ClrType)];

        foreach (IMutableEntityType ownerEntityType in modelBuilder.Model.GetEntityTypes()
            .Where(et => !svoClrTypes.Contains(et.ClrType))
            .ToList())
        {
            // Capture the SVO-typed CLR properties EF auto-discovered as navigations
            // on this owner (e.g. Party.AvatarTempId : BlobReference). We will
            // re-attach them as scalar properties once the navigation is gone.
            List<System.Reflection.PropertyInfo> svoClrProperties = [.. ownerEntityType
                .GetNavigations()
                .Where(nav => svoClrTypes.Contains(nav.TargetEntityType.ClrType))
                .Select(nav => nav.PropertyInfo)
                .OfType<System.Reflection.PropertyInfo>()];

            if (svoClrProperties.Count == 0)
            {
                continue;
            }

            // Use the builder API for both steps: `Ignore(name)` strips the
            // auto-discovered navigation (and its backing FK) cleanly, then
            // `Property(name)` re-registers the same CLR member as a scalar.
            // Doing this at builder level avoids the IMutable* surface's
            // navigation/property name conflicts and lets EF Core re-validate
            // the model after each step.
            Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder ownerBuilder =
                modelBuilder.Entity(ownerEntityType.ClrType);

            foreach (System.Reflection.PropertyInfo clrProperty in svoClrProperties)
            {
                ownerBuilder.Ignore(clrProperty.Name);
                ownerBuilder.Property(clrProperty.PropertyType, clrProperty.Name);
            }
        }

        // Now that no FK or navigation references them, the SVO entity types can be
        // removed. ApplySingleValueObjectConverters runs next and wraps each promoted
        // scalar property with the matching ValueConverter.
        foreach (IMutableEntityType svoEntityType in svoEntityTypes)
        {
            modelBuilder.Model.RemoveEntityType(svoEntityType.ClrType);
        }
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

        // FK: Translation.ParentId → Parent.Id, cascade delete (GDPR/ISO 27001 compliance)
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
            Expression activated = Expression.Property(param, nameof(IActive.Activated));
            builder.HasQueryFilter(GranitFilterNames.Active,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, activated), param));
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

        if (typeof(IHasMergeTombstone).IsAssignableFrom(typeof(TEntity)))
        {
            // Standard listings exclude tombstoned aggregates (those that have been merged
            // into another instance). Bypass via IDataFilter.Disable<IHasMergeTombstone>()
            // to surface tombstones in admin / audit views, or per-query via
            // IgnoreQueryFilters([GranitFilterNames.MergeTombstone]).
            Expression bypass = Expression.Not(
                Expression.Property(Expression.Constant(proxy), nameof(FilterProxy.MergeTombstoneEnabled)));
            Expression notTombstoned = Expression.Equal(
                Expression.Property(param, nameof(IHasMergeTombstone.MergedIntoId)),
                Expression.Constant(null, typeof(Guid?)));
            builder.HasQueryFilter(GranitFilterNames.MergeTombstone,
                Expression.Lambda<Func<TEntity, bool>>(Expression.OrElse(bypass, notTombstoned), param));
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

    // Configures the MergedIntoId / MergedAt columns + index for IHasMergeTombstone implementors.
    // The properties are declared as get-only on the interface; the implementing aggregate must
    // expose them as { get; private set; } (or private setter via reflection) for EF to populate.
    private static void ConfigureMergeTombstone<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IHasMergeTombstone
    {
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder =
            modelBuilder.Entity<TEntity>();

        builder.Property(e => e.MergedIntoId);
        builder.Property(e => e.MergedAt);

        // Index on MergedIntoId : (1) speeds up "who merged into X" lookups during chain-collapse
        // at merge time, (2) supports admin tombstone listings.
        builder.HasIndex(e => e.MergedIntoId);
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
        public bool MergeTombstoneEnabled => _dataFilter?.IsEnabled<IHasMergeTombstone>() ?? true;
    }
}
