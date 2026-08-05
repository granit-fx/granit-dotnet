// =============================================================================
// Tests - ModelBuilderExtensions
// =============================================================================
// Verifies that ApplyGranitConventions applies EF Core 10 named query filters:
//   - ISoftDeletable  → GranitFilterNames.SoftDelete
//   - IMultiTenant    → GranitFilterNames.MultiTenant
//   - IActive         → GranitFilterNames.Active
//   - IProcessingRestrictable → GranitFilterNames.ProcessingRestrictable
//   - IPublishable    → GranitFilterNames.Publishable
//   - One named filter per interface (independent, can be bypassed individually)
//   - Service-level bypass via IDataFilter
//   - Query-level bypass via IgnoreQueryFilters([GranitFilterNames.X])
//   - Backward compatibility (dataFilter = null)
//
// Note on EF Core model caching:
// EF Core caches the model by DbContext type. The filter closures (FilterProxy,
// currentTenant) captured during the first OnModelCreating call are reused for
// all subsequent instances of the same type. Tests must therefore use STATIC
// shared instances (SharedTenant, SharedDataFilter) so that mutations in tests
// affect the same objects captured in the cached model expressions.
//
// Note on multi-tenant and DataFilter tests:
// EF Core InMemory evaluates filter closures via its partial evaluator,
// which may differ from relational mode (SQL parameters). For filter behavior
// tests, the expression is compiled directly via LambdaExpression.Compile()
// and invoked without going through the EF Core pipeline.
// This tests what matters: is the closure dynamic (re-evaluated on each call)?
// =============================================================================

using System.Linq.Expressions;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class ModelBuilderExtensionsTests
{
    // Static shared instances across all tests in this class.
    // EF Core caches the model by DbContext type: the closures captured during the first
    // OnModelCreating call are reused. Using static instances ensures that test mutations
    // target the same objects held by the cached expression tree.
    private static readonly FakeCurrentTenant SharedTenant = new();
    private static readonly MutableDataFilter SharedDataFilter = new();

    // -------------------------------------------------------------------------
    // Soft delete
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyGranitConventions_FiltersSoftDeletedEntities()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Products.Add(new TestProduct { Name = "Active", IsDeleted = false });
        context.Products.Add(new TestProduct { Name = "Deleted", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, DeletedBy = "test" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestProduct> results = await context.Products.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Active");
    }

    [Fact]
    public async Task ApplyGranitConventions_IgnoreQueryFilters_ReturnsAll()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Products.Add(new TestProduct { Name = "Active", IsDeleted = false });
        context.Products.Add(new TestProduct { Name = "Deleted", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, DeletedBy = "test" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestProduct> results = await context.Products.IgnoreQueryFilters().ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ApplyGranitConventions_NonSoftDeletableEntity_NotFiltered()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Categories.Add(new TestCategory { Name = "Cat1" });
        context.Categories.Add(new TestCategory { Name = "Cat2" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestCategory> results = await context.Categories.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Multi-tenant query filter — model and expression verification
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_MultiTenant_QueryFilterIsRegisteredOnModel()
    {
        using TestMultiTenantDbContext context = CreateMultiTenantContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestTenantEntity));

        entityType.ShouldNotBeNull();
        entityType!.GetDeclaredQueryFilters()
            .Any(f => f.Key == GranitFilterNames.MultiTenant)
            .ShouldBeTrue("a named MultiTenant filter must be registered");
    }

    [Fact]
    public void ApplyGranitConventions_WithoutCurrentTenant_NoMultiTenantFilter()
    {
        using TestMultiTenantDbContextWithoutFilter context = CreateContextWithoutFilter();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestTenantEntity));

        entityType!.GetDeclaredQueryFilters()
            .Any(f => f.Key == GranitFilterNames.MultiTenant)
            .ShouldBeFalse("no MultiTenant filter must be registered without ICurrentTenant");
    }

    [Fact]
    public void GranitDbContext_MultiTenant_Filter_MatchesCurrentTenant()
    {
        // The context-bound filter uses EF.Property (not compilable in-process), so the
        // behavior is asserted through queries: only the active tenant's rows are visible.
        var tenantA = Guid.NewGuid();
        SharedTenant.Id = tenantA;

        using TestMultiTenantDbContext context = CreateMultiTenantContext();
        context.Database.EnsureCreated();
        context.TenantEntities.AddRange(
            new TestTenantEntity { Name = "mine", TenantId = tenantA },
            new TestTenantEntity { Name = "other", TenantId = Guid.NewGuid() },
            new TestTenantEntity { Name = "none", TenantId = null });
        context.SaveChanges();

        List<TestTenantEntity> visible = [.. context.TenantEntities];
        visible.ShouldHaveSingleItem("only the active tenant's rows must pass the filter");
        visible[0].Name.ShouldBe("mine");

        SharedTenant.Id = null;
    }

    [Fact]
    public void GranitDbContext_MultiTenant_Filter_EvaluatesDynamically()
    {
        // Verifies the filter re-evaluates CurrentTenantId per query on the SAME context —
        // the parameterised behavior that prevents the frozen-tenant leak.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SharedTenant.Id = tenantA;

        using TestMultiTenantDbContext context = CreateMultiTenantContext();
        context.Database.EnsureCreated();
        context.TenantEntities.AddRange(
            new TestTenantEntity { Name = "a", TenantId = tenantA },
            new TestTenantEntity { Name = "b", TenantId = tenantB });
        context.SaveChanges();

        context.TenantEntities.Single().Name.ShouldBe("a");

        SharedTenant.Id = tenantB;
        context.TenantEntities.Single().Name.ShouldBe("b", "the filter must re-evaluate after a tenant change");

        SharedTenant.Id = null;
    }

    // -------------------------------------------------------------------------
    // IActive filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyGranitConventions_FiltersInactiveEntities()
    {
        // Arrange
        await using TestDbContextWithActive context = CreateContextWithActive();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.ActiveEntities.Add(new TestActiveEntity { Name = "Active", Activated = true });
        context.ActiveEntities.Add(new TestActiveEntity { Name = "Inactive", Activated = false });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestActiveEntity> results = await context.ActiveEntities.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Active");
    }

    [Fact]
    public async Task ApplyGranitConventions_ActiveFilter_IgnoreQueryFilters_ReturnsAll()
    {
        // Arrange
        await using TestDbContextWithActive context = CreateContextWithActive();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.ActiveEntities.Add(new TestActiveEntity { Name = "Active", Activated = true });
        context.ActiveEntities.Add(new TestActiveEntity { Name = "Inactive", Activated = false });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestActiveEntity> results = await context.ActiveEntities
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // IDataFilter bypass — verified via direct expression compilation
    // (see header note on EF Core model caching and EF Core InMemory vs SQL)
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_SoftDelete_DataFilter_Bypass_EvaluatesDynamically()
    {
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);

        using TestDbContextWithDataFilter context = CreateContextWithDataFilter();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestSoftDeleteWithFilter))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.SoftDelete)?.Expression;
        filter.ShouldNotBeNull();

        var compiled = (Func<TestSoftDeleteWithFilter, bool>)filter!.Compile();

        // Filter active — deleted entity excluded
        compiled(new TestSoftDeleteWithFilter { IsDeleted = true }).ShouldBeFalse("deleted must be filtered");
        compiled(new TestSoftDeleteWithFilter { IsDeleted = false }).ShouldBeTrue("non-deleted must pass");

        // Bypass — all entities pass (same compiled expression, dynamic re-evaluation)
        SharedDataFilter.SetEnabled<ISoftDeletable>(false);
        compiled(new TestSoftDeleteWithFilter { IsDeleted = true }).ShouldBeTrue("deleted must pass when filter disabled");
        compiled(new TestSoftDeleteWithFilter { IsDeleted = false }).ShouldBeTrue("non-deleted must pass when filter disabled");

        // Restore — filter active again
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);
        compiled(new TestSoftDeleteWithFilter { IsDeleted = true }).ShouldBeFalse("filter must be restored");
    }

    [Fact]
    public void ApplyGranitConventions_Active_DataFilter_Bypass_EvaluatesDynamically()
    {
        SharedDataFilter.SetEnabled<IActive>(true);

        using TestDbContextWithDataFilter context = CreateContextWithDataFilter();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestActiveWithFilter))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.Active)?.Expression;
        filter.ShouldNotBeNull();

        var compiled = (Func<TestActiveWithFilter, bool>)filter!.Compile();

        compiled(new TestActiveWithFilter { Activated = false }).ShouldBeFalse("inactive must be filtered");
        compiled(new TestActiveWithFilter { Activated = true }).ShouldBeTrue("active must pass");

        SharedDataFilter.SetEnabled<IActive>(false);
        compiled(new TestActiveWithFilter { Activated = false }).ShouldBeTrue("inactive must pass when filter disabled");

        SharedDataFilter.SetEnabled<IActive>(true);
    }

    // -------------------------------------------------------------------------
    // IProcessingRestrictable filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyGranitConventions_FiltersProcessingRestrictedEntities()
    {
        // Arrange
        await using TestDbContextWithProcessingRestrictable context = CreateContextWithProcessingRestrictable();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.RestrictableEntities.Add(new TestProcessingRestrictableEntity { Name = "Normal", IsProcessingRestricted = false });
        context.RestrictableEntities.Add(new TestProcessingRestrictableEntity
        {
            Name = "Restricted",
            IsProcessingRestricted = true,
            ProcessingRestrictedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
            ProcessingRestrictedBy = "dpo",
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestProcessingRestrictableEntity> results = await context.RestrictableEntities.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("Normal");
    }

    [Fact]
    public async Task ApplyGranitConventions_ProcessingRestrictable_IgnoreQueryFilters_ReturnsAll()
    {
        // Arrange
        await using TestDbContextWithProcessingRestrictable context = CreateContextWithProcessingRestrictable();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.RestrictableEntities.Add(new TestProcessingRestrictableEntity { Name = "Normal", IsProcessingRestricted = false });
        context.RestrictableEntities.Add(new TestProcessingRestrictableEntity { Name = "Restricted", IsProcessingRestricted = true });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        List<TestProcessingRestrictableEntity> results = await context.RestrictableEntities
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyGranitConventions_ProcessingRestrictable_DataFilter_Bypass_EvaluatesDynamically()
    {
        SharedDataFilter.SetEnabled<IProcessingRestrictable>(true);

        using TestDbContextWithProcessingRestrictableDataFilter context = CreateContextWithProcessingRestrictableDataFilter();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestProcessingRestrictableWithFilter))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.ProcessingRestrictable)?.Expression;
        filter.ShouldNotBeNull();

        var compiled =
            (Func<TestProcessingRestrictableWithFilter, bool>)filter!.Compile();

        // Filter active — restricted entity excluded
        compiled(new TestProcessingRestrictableWithFilter { IsProcessingRestricted = true }).ShouldBeFalse("restricted must be filtered");
        compiled(new TestProcessingRestrictableWithFilter { IsProcessingRestricted = false }).ShouldBeTrue("non-restricted must pass");

        // Bypass — all entities pass
        SharedDataFilter.SetEnabled<IProcessingRestrictable>(false);
        compiled(new TestProcessingRestrictableWithFilter { IsProcessingRestricted = true }).ShouldBeTrue("restricted must pass when filter disabled");

        // Restore
        SharedDataFilter.SetEnabled<IProcessingRestrictable>(true);
        compiled(new TestProcessingRestrictableWithFilter { IsProcessingRestricted = true }).ShouldBeFalse("filter must be restored");
    }

    [Fact]
    public void ApplyGranitConventions_CombinedSoftDeleteAndProcessingRestrictable_HasTwoNamedFilters()
    {
        using TestDbContextWithCombinedSdPr context = CreateContextWithCombinedSdPr();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestCombinedSdPrEntity));

        entityType.ShouldNotBeNull();
        IReadOnlyCollection<IQueryFilter> filters = entityType!.GetDeclaredQueryFilters();
        filters.Count.ShouldBe(2, "one named filter per interface: SoftDelete + ProcessingRestrictable");
        filters.Any(f => f.Key == GranitFilterNames.SoftDelete).ShouldBeTrue();
        filters.Any(f => f.Key == GranitFilterNames.ProcessingRestrictable).ShouldBeTrue();
    }

    [Fact]
    public void ApplyGranitConventions_CombinedSoftDeleteAndProcessingRestrictable_BothFiltersActive()
    {
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);
        SharedDataFilter.SetEnabled<IProcessingRestrictable>(true);

        using TestDbContextWithCombinedSdPr context = CreateContextWithCombinedSdPr();
        IReadOnlyCollection<IQueryFilter> filters = context.Model
            .FindEntityType(typeof(TestCombinedSdPrEntity))!.GetDeclaredQueryFilters();

        var compiledSd = (Func<TestCombinedSdPrEntity, bool>)filters.First(f => f.Key == GranitFilterNames.SoftDelete).Expression!.Compile();
        var compiledPr = (Func<TestCombinedSdPrEntity, bool>)filters.First(f => f.Key == GranitFilterNames.ProcessingRestrictable).Expression!.Compile();

        // Combined = both must pass (EF Core ANDs named filters at query time)
        // We test each individually and verify the expected behavior
        bool passes(TestCombinedSdPrEntity e) => compiledSd(e) && compiledPr(e);

        // Not deleted, not restricted — passes both
        passes(new TestCombinedSdPrEntity { IsDeleted = false, IsProcessingRestricted = false }).ShouldBeTrue();

        // Deleted — excluded by soft delete filter
        passes(new TestCombinedSdPrEntity { IsDeleted = true, IsProcessingRestricted = false }).ShouldBeFalse("deleted must be filtered");

        // Restricted — excluded by processing restriction filter
        passes(new TestCombinedSdPrEntity { IsDeleted = false, IsProcessingRestricted = true }).ShouldBeFalse("restricted must be filtered");

        // Both — excluded
        passes(new TestCombinedSdPrEntity { IsDeleted = true, IsProcessingRestricted = true }).ShouldBeFalse("both must be filtered");
    }

    [Fact]
    public void ApplyGranitConventions_CombinedSoftDeleteAndProcessingRestrictable_IndependentBypass()
    {
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);
        SharedDataFilter.SetEnabled<IProcessingRestrictable>(false); // processing restriction bypassed

        using TestDbContextWithCombinedSdPr context = CreateContextWithCombinedSdPr();
        IReadOnlyCollection<IQueryFilter> filters = context.Model
            .FindEntityType(typeof(TestCombinedSdPrEntity))!.GetDeclaredQueryFilters();

        var compiledSd = (Func<TestCombinedSdPrEntity, bool>)filters.First(f => f.Key == GranitFilterNames.SoftDelete).Expression!.Compile();
        var compiledPr = (Func<TestCombinedSdPrEntity, bool>)filters.First(f => f.Key == GranitFilterNames.ProcessingRestrictable).Expression!.Compile();

        // Processing restriction bypassed — compiledPr passes everything
        compiledPr(new TestCombinedSdPrEntity { IsProcessingRestricted = true }).ShouldBeTrue("restriction bypassed");

        // Soft delete still active — deleted entity excluded by compiledSd
        compiledSd(new TestCombinedSdPrEntity { IsDeleted = true }).ShouldBeFalse("soft delete still active");

        SharedDataFilter.SetEnabled<IProcessingRestrictable>(true);
    }

    // -------------------------------------------------------------------------
    // Bug fix: entities combining multiple filter interfaces
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_CombinedEntity_HasTwoNamedFilters()
    {
        // SharedTenant is non-null so both ISoftDeletable and IMultiTenant filters are registered
        using TestDbContextWithCombined context = CreateContextWithCombined();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestCombinedEntity));

        entityType.ShouldNotBeNull();
        IReadOnlyCollection<IQueryFilter> filters = entityType!.GetDeclaredQueryFilters();
        filters.Count.ShouldBe(2, "one named filter per interface: SoftDelete + MultiTenant");
        filters.Any(f => f.Key == GranitFilterNames.SoftDelete).ShouldBeTrue();
        filters.Any(f => f.Key == GranitFilterNames.MultiTenant).ShouldBeTrue();
    }

    [Fact]
    public void GranitDbContext_CombinedEntity_BothFiltersActive()
    {
        var tenantA = Guid.NewGuid();
        SharedTenant.Id = tenantA;
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);
        SharedDataFilter.SetEnabled<IMultiTenant>(true);

        using TestDbContextWithCombined context = CreateContextWithCombined();
        context.Database.EnsureCreated();
        context.CombinedEntities.AddRange(
            new TestCombinedEntity { TenantId = tenantA, IsDeleted = false },
            new TestCombinedEntity { TenantId = tenantA, IsDeleted = true },
            new TestCombinedEntity { TenantId = Guid.NewGuid(), IsDeleted = false });
        context.SaveChanges();

        // Only the current-tenant, non-deleted row survives both filters.
        context.CombinedEntities.Count().ShouldBe(1);

        SharedTenant.Id = null;
    }

    [Fact]
    public void GranitDbContext_CombinedEntity_IndependentBypass()
    {
        var tenantA = Guid.NewGuid();
        SharedTenant.Id = tenantA;
        SharedDataFilter.SetEnabled<ISoftDeletable>(false); // soft delete bypassed
        SharedDataFilter.SetEnabled<IMultiTenant>(true);    // multi-tenant active

        using TestDbContextWithCombined context = CreateContextWithCombined();
        context.Database.EnsureCreated();
        context.CombinedEntities.AddRange(
            new TestCombinedEntity { TenantId = tenantA, IsDeleted = true },
            new TestCombinedEntity { TenantId = Guid.NewGuid(), IsDeleted = true });
        context.SaveChanges();

        // Soft delete bypassed → the deleted current-tenant row is visible;
        // multi-tenant still active → the other tenant's row stays hidden.
        context.CombinedEntities.Count().ShouldBe(1);

        SharedTenant.Id = null;
        SharedDataFilter.SetEnabled<ISoftDeletable>(true);
    }

    // -------------------------------------------------------------------------
    // Backward compatibility — dataFilter = null
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_NullDataFilter_FiltersAlwaysApply()
    {
        using TestDbContext context = CreateContext();
        LambdaExpression? filter = context.Model
            .FindEntityType(typeof(TestProduct))
            ?.GetDeclaredQueryFilters()
            .FirstOrDefault(f => f.Key == GranitFilterNames.SoftDelete)?.Expression;
        filter.ShouldNotBeNull("soft delete filter must be registered even without IDataFilter");

        var compiled = (Func<TestProduct, bool>)filter!.Compile();

        // Assert — filter always active
        compiled(new TestProduct { IsDeleted = true }).ShouldBeFalse("deleted must be filtered");
        compiled(new TestProduct { IsDeleted = false }).ShouldBeTrue("non-deleted must pass");
    }

    // -------------------------------------------------------------------------
    // Per-query named filter bypass (EF Core 10)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyGranitConventions_IgnoreQueryFilter_ByName_ReturnsDeleted()
    {
        // IgnoreQueryFilters([name]) bypasses only the named filter, leaving others active
        await using TestDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Products.Add(new TestProduct { Name = "Active", IsDeleted = false });
        context.Products.Add(new TestProduct { Name = "Deleted", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, DeletedBy = "test" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Bypass only the SoftDelete filter — equivalent to IgnoreQueryFilters() but scoped
        List<TestProduct> results = await context.Products
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2, "SoftDelete filter bypassed per-query — both records returned");
    }

    // -------------------------------------------------------------------------
    // IConcurrencyAware — concurrency token property configuration
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_ConcurrencyAware_ConfiguresConcurrencyToken()
    {
        using TestDbContextWithConcurrencyAware context = CreateContextWithConcurrencyAware();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType =
            context.Model.FindEntityType(typeof(TestConcurrencyAwareEntity));

        entityType.ShouldNotBeNull();
        Microsoft.EntityFrameworkCore.Metadata.IProperty? property =
            entityType!.FindProperty(nameof(IConcurrencyAware.ConcurrencyStamp));

        property.ShouldNotBeNull("ConcurrencyStamp property must exist on the model");
        property!.IsConcurrencyToken.ShouldBeTrue("ConcurrencyStamp must be configured as a concurrency token");
        property.GetMaxLength().ShouldBe(36, "ConcurrencyStamp must be VARCHAR(36)");
    }

    [Fact]
    public void ApplyGranitConventions_NonConcurrencyAware_NoConcurrencyToken()
    {
        using TestDbContext context = CreateContext();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType =
            context.Model.FindEntityType(typeof(TestCategory));

        entityType.ShouldNotBeNull();
        Microsoft.EntityFrameworkCore.Metadata.IProperty? property =
            entityType!.FindProperty("ConcurrencyStamp");

        property.ShouldBeNull("non-IConcurrencyAware entity must not have ConcurrencyStamp");
    }

    // -------------------------------------------------------------------------
    // IHasMergeTombstone — auto-column + index + named query filter
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyGranitConventions_Mergeable_AddsMergedIntoIdProperty()
    {
        using TestDbContextWithMergeable context = CreateMergeableContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestMergeableEntity));
        entityType.ShouldNotBeNull();

        Microsoft.EntityFrameworkCore.Metadata.IProperty? mergedIntoId =
            entityType!.FindProperty(nameof(IHasMergeTombstone.MergedIntoId));
        mergedIntoId.ShouldNotBeNull("MergedIntoId must be auto-mapped on IHasMergeTombstone implementors");
        mergedIntoId!.ClrType.ShouldBe(typeof(Guid?));
        mergedIntoId.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void ApplyGranitConventions_Mergeable_AddsMergedAtProperty()
    {
        using TestDbContextWithMergeable context = CreateMergeableContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TestMergeableEntity));
        Microsoft.EntityFrameworkCore.Metadata.IProperty? mergedAt =
            entityType!.FindProperty(nameof(IHasMergeTombstone.MergedAt));
        mergedAt.ShouldNotBeNull();
        mergedAt!.ClrType.ShouldBe(typeof(DateTimeOffset?));
        mergedAt.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void ApplyGranitConventions_Mergeable_AddsIndexOnMergedIntoId()
    {
        using TestDbContextWithMergeable context = CreateMergeableContext();

        IEntityType entityType = context.Model.FindEntityType(typeof(TestMergeableEntity))!;
        bool hasIndex = entityType.GetIndexes()
            .Any(idx => idx.Properties.Count == 1
                        && idx.Properties[0].Name == nameof(IHasMergeTombstone.MergedIntoId));
        hasIndex.ShouldBeTrue("an index on MergedIntoId is required for tombstone lookups");
    }

    [Fact]
    public void ApplyGranitConventions_Mergeable_RegistersNamedQueryFilter()
    {
        using TestDbContextWithMergeable context = CreateMergeableContext();

        IEntityType entityType = context.Model.FindEntityType(typeof(TestMergeableEntity))!;
        bool hasFilter = entityType.GetDeclaredQueryFilters()
            .Any(f => f.Key == GranitFilterNames.MergeTombstone);
        hasFilter.ShouldBeTrue("a named MergeTombstone filter must be registered");
    }

    [Fact]
    public async Task ApplyGranitConventions_Mergeable_FiltersTombstonedEntities()
    {
        await using TestDbContextWithMergeable context = CreateMergeableContext();

        var alive = new TestMergeableEntity { Name = "alive" };
        var tombstoned = new TestMergeableEntity
        {
            Name = "tombstoned",
            MergedIntoId = alive.Id,
            MergedAt = DateTimeOffset.UtcNow,
        };
        context.Mergeables.AddRange(alive, tombstoned);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<TestMergeableEntity> visible = await context.Mergeables
            .ToListAsync(TestContext.Current.CancellationToken);
        visible.ShouldHaveSingleItem().Name.ShouldBe("alive");

        List<TestMergeableEntity> all = await context.Mergeables
            .IgnoreQueryFilters([GranitFilterNames.MergeTombstone])
            .ToListAsync(TestContext.Current.CancellationToken);
        all.Count.ShouldBe(2);
    }

    private static TestDbContextWithMergeable CreateMergeableContext()
    {
        DbContextOptions<TestDbContextWithMergeable> options =
            new DbContextOptionsBuilder<TestDbContextWithMergeable>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new TestDbContextWithMergeable(options);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }

    private static TestMultiTenantDbContext CreateMultiTenantContext()
    {
        DbContextOptions<TestMultiTenantDbContext> options = new DbContextOptionsBuilder<TestMultiTenantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TestMultiTenantDbContext(options, SharedTenant);
    }

    private static TestMultiTenantDbContextWithoutFilter CreateContextWithoutFilter()
    {
        DbContextOptions<TestMultiTenantDbContextWithoutFilter> options =
            new DbContextOptionsBuilder<TestMultiTenantDbContextWithoutFilter>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestMultiTenantDbContextWithoutFilter(options);
    }

    private static TestDbContextWithActive CreateContextWithActive()
    {
        DbContextOptions<TestDbContextWithActive> options =
            new DbContextOptionsBuilder<TestDbContextWithActive>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithActive(options);
    }

    // Always uses the static SharedDataFilter — required so that test mutations target
    // the same instance captured in the EF Core cached model expression.
    private static TestDbContextWithDataFilter CreateContextWithDataFilter()
    {
        DbContextOptions<TestDbContextWithDataFilter> options =
            new DbContextOptionsBuilder<TestDbContextWithDataFilter>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithDataFilter(options, SharedDataFilter);
    }

    private static TestDbContextWithProcessingRestrictable CreateContextWithProcessingRestrictable()
    {
        DbContextOptions<TestDbContextWithProcessingRestrictable> options =
            new DbContextOptionsBuilder<TestDbContextWithProcessingRestrictable>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithProcessingRestrictable(options);
    }

    private static TestDbContextWithProcessingRestrictableDataFilter CreateContextWithProcessingRestrictableDataFilter()
    {
        DbContextOptions<TestDbContextWithProcessingRestrictableDataFilter> options =
            new DbContextOptionsBuilder<TestDbContextWithProcessingRestrictableDataFilter>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithProcessingRestrictableDataFilter(options, SharedDataFilter);
    }

    private static TestDbContextWithCombinedSdPr CreateContextWithCombinedSdPr()
    {
        DbContextOptions<TestDbContextWithCombinedSdPr> options =
            new DbContextOptionsBuilder<TestDbContextWithCombinedSdPr>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithCombinedSdPr(options, SharedDataFilter);
    }

    private static TestDbContextWithConcurrencyAware CreateContextWithConcurrencyAware()
    {
        DbContextOptions<TestDbContextWithConcurrencyAware> options =
            new DbContextOptionsBuilder<TestDbContextWithConcurrencyAware>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithConcurrencyAware(options);
    }

    // Always uses SharedTenant and SharedDataFilter — same reason as above.
    private static TestDbContextWithCombined CreateContextWithCombined()
    {
        DbContextOptions<TestDbContextWithCombined> options =
            new DbContextOptionsBuilder<TestDbContextWithCombined>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new TestDbContextWithCombined(options, SharedTenant, SharedDataFilter);
    }

    // Mutable IDataFilter for tests: direct control without AsyncLocal.
    // Verifies that filter closures are dynamic (re-evaluated on each call).
    private sealed class MutableDataFilter : IDataFilter
    {
        private readonly Dictionary<Type, bool> _state = [];

        public void SetEnabled<TFilter>(bool enabled) => _state[typeof(TFilter)] = enabled;

        public IDisposable Disable<TFilter>() where TFilter : class
        {
            _state[typeof(TFilter)] = false;
            return NullScope.Instance;
        }

        public IDisposable Enable<TFilter>() where TFilter : class
        {
            _state[typeof(TFilter)] = true;
            return NullScope.Instance;
        }

        public bool IsEnabled<TFilter>() where TFilter : class =>
            !_state.TryGetValue(typeof(TFilter), out bool value) || value;

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

#region Test entities

internal sealed class TestProduct : ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class TestTenantEntity : IMultiTenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
}

internal sealed class TestActiveEntity : IActive
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Activated { get; set; }
}

internal sealed class TestSoftDeleteWithFilter : ISoftDeletable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

internal sealed class TestActiveWithFilter : IActive
{
    public int Id { get; set; }
    public bool Activated { get; set; }
}

internal sealed class TestCombinedEntity : ISoftDeletable, IMultiTenant
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public Guid? TenantId { get; set; }
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestProduct> Products => Set<TestProduct>();
    public DbSet<TestCategory> Categories => Set<TestCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

// GranitDbContext derivative: since #3162 the tenant filter is ONLY registered by the base
// class (this-bound, parameterised) — the legacy ApplyGranitConventions tenant overload is gone.
internal sealed class TestMultiTenantDbContext(DbContextOptions<TestMultiTenantDbContext> options, ICurrentTenant currentTenant)
    : GranitDbContext(options, currentTenant)
{
    public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();
}

// DbContext without multi-tenant filter (currentTenant not provided)
internal sealed class TestMultiTenantDbContextWithoutFilter(DbContextOptions<TestMultiTenantDbContextWithoutFilter> options) : DbContext(options)
{
    public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

internal sealed class TestDbContextWithActive(DbContextOptions<TestDbContextWithActive> options) : DbContext(options)
{
    public DbSet<TestActiveEntity> ActiveEntities => Set<TestActiveEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

internal sealed class TestDbContextWithDataFilter(
    DbContextOptions<TestDbContextWithDataFilter> options,
    IDataFilter dataFilter) : DbContext(options)
{
    private readonly IDataFilter _dataFilter = dataFilter;

    public DbSet<TestSoftDeleteWithFilter> SoftDeleteEntities => Set<TestSoftDeleteWithFilter>();
    public DbSet<TestActiveWithFilter> ActiveEntities => Set<TestActiveWithFilter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions(dataFilter: _dataFilter);
}

internal sealed class TestDbContextWithCombined(
    DbContextOptions<TestDbContextWithCombined> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<TestCombinedEntity> CombinedEntities => Set<TestCombinedEntity>();
}

internal sealed class TestProcessingRestrictableEntity : IProcessingRestrictable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsProcessingRestricted { get; set; }
    public DateTimeOffset? ProcessingRestrictedAt { get; set; }
    public string? ProcessingRestrictedBy { get; set; }
}

internal sealed class TestProcessingRestrictableWithFilter : IProcessingRestrictable
{
    public int Id { get; set; }
    public bool IsProcessingRestricted { get; set; }
    public DateTimeOffset? ProcessingRestrictedAt { get; set; }
    public string? ProcessingRestrictedBy { get; set; }
}

internal sealed class TestCombinedSdPrEntity : ISoftDeletable, IProcessingRestrictable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsProcessingRestricted { get; set; }
    public DateTimeOffset? ProcessingRestrictedAt { get; set; }
    public string? ProcessingRestrictedBy { get; set; }
}

internal sealed class TestDbContextWithProcessingRestrictable(DbContextOptions<TestDbContextWithProcessingRestrictable> options) : DbContext(options)
{
    public DbSet<TestProcessingRestrictableEntity> RestrictableEntities => Set<TestProcessingRestrictableEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

internal sealed class TestDbContextWithProcessingRestrictableDataFilter(
    DbContextOptions<TestDbContextWithProcessingRestrictableDataFilter> options,
    IDataFilter dataFilter) : DbContext(options)
{
    private readonly IDataFilter _dataFilter = dataFilter;

    public DbSet<TestProcessingRestrictableWithFilter> RestrictableEntities => Set<TestProcessingRestrictableWithFilter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions(dataFilter: _dataFilter);
}

internal sealed class TestDbContextWithCombinedSdPr(
    DbContextOptions<TestDbContextWithCombinedSdPr> options,
    IDataFilter dataFilter) : DbContext(options)
{
    private readonly IDataFilter _dataFilter = dataFilter;

    public DbSet<TestCombinedSdPrEntity> CombinedSdPrEntities => Set<TestCombinedSdPrEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions(dataFilter: _dataFilter);
}

internal sealed class TestConcurrencyAwareEntity : IConcurrencyAware
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

internal sealed class TestDbContextWithConcurrencyAware(DbContextOptions<TestDbContextWithConcurrencyAware> options) : DbContext(options)
{
    public DbSet<TestConcurrencyAwareEntity> ConcurrencyAwareEntities => Set<TestConcurrencyAwareEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

internal sealed class TestMergeableEntity : IHasMergeTombstone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? MergedIntoId { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
}

internal sealed class TestDbContextWithMergeable(DbContextOptions<TestDbContextWithMergeable> options) : DbContext(options)
{
    public DbSet<TestMergeableEntity> Mergeables => Set<TestMergeableEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyGranitConventions();
}

#endregion Test entities
