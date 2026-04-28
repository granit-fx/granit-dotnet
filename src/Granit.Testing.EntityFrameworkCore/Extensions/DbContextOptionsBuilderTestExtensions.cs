using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods on <see cref="DbContextOptionsBuilder{TContext}"/> that opt tests
/// out of EF Core defaults that are correct in production but trap multi-tenant tests.
/// </summary>
public static class DbContextOptionsBuilderTestExtensions
{
    /// <summary>
    /// Forces EF Core to allocate a dedicated internal service provider — and therefore a
    /// dedicated model cache — for this <see cref="DbContextOptions"/>. Required in tests
    /// that exercise the Granit multi-tenant query filter with a per-test
    /// <see cref="Granit.MultiTenancy.ICurrentTenant"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The trap.</b> <c>ApplyGranitConventions</c> compiles the multi-tenant filter as
    /// <c>e =&gt; e.TenantId == currentTenant.Id</c>, capturing the <c>ICurrentTenant</c>
    /// instance via <see cref="System.Linq.Expressions.Expression.Constant(object)"/>.
    /// EF Core re-reads <c>currentTenant.Id</c> on every query — but always from the
    /// instance captured at model-build time. EF's default model cache is keyed on
    /// <c>(DbContextType, designTime)</c> and shared process-wide, so the first test that
    /// builds a model pins its tenant instance for the whole run. Subsequent tests get
    /// fresh mocks, but EF reuses the cached expression — queries silently filter against
    /// the wrong (or stranded) tenant and return zero rows.
    /// </para>
    /// <para>
    /// <b>Why production is fine.</b> A single DI-scoped <c>ICurrentTenant</c> is shared
    /// across all queries, and its mutable AsyncLocal state already reflects the active
    /// request. Per-instance capture is an optimization, not a bug — only xUnit's
    /// "fresh test class instance per <c>[Fact]</c>" lifecycle exposes the gap.
    /// </para>
    /// <para>
    /// <b>What this method does.</b> Calls
    /// <see cref="DbContextOptionsBuilder.EnableServiceProviderCaching(bool)"/> with
    /// <c>false</c>. Each <see cref="DbContextOptions"/> gets its own EF service provider
    /// (and therefore its own model), so <c>OnModelCreating</c> re-runs and captures the
    /// current test's tenant. Trivial overhead in tests; never use in production.
    /// </para>
    /// <para>
    /// <b>Alternative pattern.</b> Reuse a single mutable
    /// <see cref="Granit.Testing.Fakes.FakeCurrentTenant"/> across tests in a class fixture
    /// — its AsyncLocal-backed state is per-test-isolated, and a single captured instance
    /// is fine because mutation of the AsyncLocal slot is what the filter reads. Either
    /// approach works; this method is the safer default for tests that allocate a fresh
    /// mock per <c>[Fact]</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type being configured.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder<TContext> EnableGranitTestModelIsolation<TContext>(
        this DbContextOptionsBuilder<TContext> builder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.EnableServiceProviderCaching(false);
    }
}
