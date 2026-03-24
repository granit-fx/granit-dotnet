// =============================================================================
// Tests - DataFilter
// =============================================================================
// Verifies the AsyncLocal implementation of IDataFilter:
//   - Default state: all filters enabled
//   - Disable/Enable create scopes that restore the previous state
//   - Nested scopes correctly restore state
//   - Filter types are independent of each other
//   - AsyncLocal isolation between parallel tasks
// =============================================================================

using Granit.DataFiltering;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.DataFiltering;

public sealed class DataFilterTests
{
    // Marker interfaces used as filter type identifiers — independent from production types.
    private interface IFilterA;
    private interface IFilterB;

    private static DataFilter Create() => new();

    // -------------------------------------------------------------------------
    // Default state
    // -------------------------------------------------------------------------

    [Fact]
    public void IsEnabled_ByDefault_ReturnsTrue()
    {
        DataFilter filter = Create();

        filter.IsEnabled<ISoftDeletable>().ShouldBeTrue();
        filter.IsEnabled<IMultiTenant>().ShouldBeTrue();
        filter.IsEnabled<IActive>().ShouldBeTrue();
        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Disable
    // -------------------------------------------------------------------------

    [Fact]
    public void Disable_MakesIsEnabledReturnFalse()
    {
        DataFilter filter = Create();

        using IDisposable scope = filter.Disable<IFilterA>();

        filter.IsEnabled<IFilterA>().ShouldBeFalse();
    }

    [Fact]
    public void Disable_Dispose_RestoresPreviousState()
    {
        DataFilter filter = Create();

        IDisposable scope = filter.Disable<IFilterA>();
        scope.Dispose();

        filter.IsEnabled<IFilterA>().ShouldBeTrue("scope disposed must restore enabled state");
    }

    // -------------------------------------------------------------------------
    // Enable
    // -------------------------------------------------------------------------

    [Fact]
    public void Enable_AfterDisable_MakesIsEnabledReturnTrue()
    {
        DataFilter filter = Create();

        using IDisposable disableScope = filter.Disable<IFilterA>();
        using IDisposable enableScope = filter.Enable<IFilterA>();

        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    [Fact]
    public void Enable_Dispose_RestoresPreviousDisabledState()
    {
        DataFilter filter = Create();

        using IDisposable disableScope = filter.Disable<IFilterA>();

        IDisposable enableScope = filter.Enable<IFilterA>();
        filter.IsEnabled<IFilterA>().ShouldBeTrue();

        enableScope.Dispose();
        filter.IsEnabled<IFilterA>().ShouldBeFalse("re-disable state must be restored after enable scope disposed");
    }

    // -------------------------------------------------------------------------
    // Nested scopes
    // -------------------------------------------------------------------------

    [Fact]
    public void NestedScopes_RestoreCorrectly()
    {
        DataFilter filter = Create();

        // Level 0: enabled (default)
        filter.IsEnabled<IFilterA>().ShouldBeTrue();

        IDisposable disableScope = filter.Disable<IFilterA>();
        // Level 1: disabled
        filter.IsEnabled<IFilterA>().ShouldBeFalse();

        IDisposable reenableScope = filter.Enable<IFilterA>();
        // Level 2: re-enabled
        filter.IsEnabled<IFilterA>().ShouldBeTrue();

        reenableScope.Dispose();
        // Back to level 1: disabled
        filter.IsEnabled<IFilterA>().ShouldBeFalse();

        disableScope.Dispose();
        // Back to level 0: enabled
        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Idempotent Dispose
    // -------------------------------------------------------------------------

    [Fact]
    public void Dispose_IsIdempotent()
    {
        DataFilter filter = Create();

        IDisposable scope = filter.Disable<IFilterA>();
        scope.Dispose();
        scope.Dispose(); // must not throw or corrupt state

        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Filter type independence
    // -------------------------------------------------------------------------

    [Fact]
    public void Disable_OneFilter_DoesNotAffectOtherFilters()
    {
        DataFilter filter = Create();

        using IDisposable scope = filter.Disable<IFilterA>();

        filter.IsEnabled<IFilterA>().ShouldBeFalse();
        filter.IsEnabled<IFilterB>().ShouldBeTrue("IFilterB must be independent from IFilterA");
        filter.IsEnabled<ISoftDeletable>().ShouldBeTrue("ISoftDeletable must be independent");
        filter.IsEnabled<IMultiTenant>().ShouldBeTrue("IMultiTenant must be independent");
        filter.IsEnabled<IActive>().ShouldBeTrue("IActive must be independent");
    }

    [Fact]
    public void MultipleFilters_CanBeDisabledIndependently()
    {
        DataFilter filter = Create();

        using IDisposable scopeA = filter.Disable<IFilterA>();
        using IDisposable scopeB = filter.Disable<IFilterB>();

        filter.IsEnabled<IFilterA>().ShouldBeFalse();
        filter.IsEnabled<IFilterB>().ShouldBeFalse();
    }

    [Fact]
    public void MultipleFilters_DisposeRestoresEachIndependently()
    {
        DataFilter filter = Create();

        IDisposable scopeA = filter.Disable<IFilterA>();
        IDisposable scopeB = filter.Disable<IFilterB>();

        scopeB.Dispose();
        filter.IsEnabled<IFilterA>().ShouldBeFalse("IFilterA must still be disabled");
        filter.IsEnabled<IFilterB>().ShouldBeTrue("IFilterB must be restored");

        scopeA.Dispose();
        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // AsyncLocal isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AsyncLocal_ParallelTasks_HaveIsolatedState()
    {
        DataFilter filter = Create();

        var taskA = Task.Run(async () =>
        {
            using IDisposable scope = filter.Disable<IFilterA>();
            await Task.Delay(20, TestContext.Current.CancellationToken);
            filter.IsEnabled<IFilterA>().ShouldBeFalse("task A must see its own disabled state");
        }, TestContext.Current.CancellationToken);

        var taskB = Task.Run(async () =>
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
            // Task B never disabled IFilterA — must see the default state.
            filter.IsEnabled<IFilterA>().ShouldBeTrue("task B must see the default enabled state");
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(taskA, taskB);

        // Root flow state is untouched.
        filter.IsEnabled<IFilterA>().ShouldBeTrue();
    }

    [Fact]
    public async Task AsyncLocal_ChildTask_InheritsParentStateSnapshot_ButIsIsolated()
    {
        // AsyncLocal: child tasks inherit the parent's value AT the moment of Task.Run(),
        // but mutations in the child do NOT propagate back to the parent.
        DataFilter filter = Create();

        using IDisposable parentScope = filter.Disable<IFilterA>();

        await Task.Run(async () =>
        {
            // Child inherits parent's disabled state.
            filter.IsEnabled<IFilterA>().ShouldBeFalse("child inherits parent's snapshot");

            // Child re-enables — does not affect parent.
            using IDisposable childScope = filter.Enable<IFilterA>();
            filter.IsEnabled<IFilterA>().ShouldBeTrue("child re-enabled in its own flow");

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        // Parent state is unaffected by the child's mutation.
        filter.IsEnabled<IFilterA>().ShouldBeFalse("parent state must be unchanged after child task");
    }
}
