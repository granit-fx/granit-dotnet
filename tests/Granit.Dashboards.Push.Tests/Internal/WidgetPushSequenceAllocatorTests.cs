using Granit.Dashboards.Push.Internal;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.Tests.Internal;

/// <summary>
/// Pins the sequence-allocation contract from ADR-043 §5: monotonic per
/// <c>(tenantId, widgetInstanceId)</c>, starts at 1 (matching pull-mode seed),
/// independent counters per key.
/// </summary>
public sealed class WidgetPushSequenceAllocatorTests
{
    [Fact]
    public void Next_StartsAtOne_ForFreshKey()
    {
        var allocator = new WidgetPushSequenceAllocator();
        var widget = Guid.NewGuid();

        allocator.Next(tenantId: null, widgetInstanceId: widget).ShouldBe(1L);
    }

    [Fact]
    public void Next_IncrementsMonotonically_ForSameKey()
    {
        var allocator = new WidgetPushSequenceAllocator();
        var widget = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        allocator.Next(tenant, widget).ShouldBe(1L);
        allocator.Next(tenant, widget).ShouldBe(2L);
        allocator.Next(tenant, widget).ShouldBe(3L);
    }

    [Fact]
    public void Next_PartitionsPerWidgetInstance()
    {
        var allocator = new WidgetPushSequenceAllocator();
        var tenant = Guid.NewGuid();
        var widgetA = Guid.NewGuid();
        var widgetB = Guid.NewGuid();

        allocator.Next(tenant, widgetA).ShouldBe(1L);
        allocator.Next(tenant, widgetB).ShouldBe(1L);                        // independent of widgetA
        allocator.Next(tenant, widgetA).ShouldBe(2L);
        allocator.Next(tenant, widgetB).ShouldBe(2L);
    }

    [Fact]
    public void Next_PartitionsPerTenant()
    {
        var allocator = new WidgetPushSequenceAllocator();
        var widget = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        allocator.Next(tenantA, widget).ShouldBe(1L);
        allocator.Next(tenantB, widget).ShouldBe(1L);                        // independent of tenantA — multi-tenant isolation
        allocator.Next(tenantA, widget).ShouldBe(2L);
        allocator.Next(null, widget).ShouldBe(1L);                           // null tenant is its own partition
    }

    [Fact]
    public async Task Next_IsThreadSafe_UnderConcurrentAccess()
    {
        // Stress the allocator under concurrent reads on the same key. The
        // returned sequences must form the contiguous range 1..N — any drop
        // means Interlocked.Increment lost a write, any duplicate means two
        // threads observed the same value.
        var allocator = new WidgetPushSequenceAllocator();
        var widget = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        const int CallsPerThread = 1000;
        const int Threads = 8;

        long[] results = new long[Threads * CallsPerThread];

        Task[] tasks = [.. Enumerable.Range(0, Threads).Select(t => Task.Run(() =>
        {
            int basis = t * CallsPerThread;
            for (int i = 0; i < CallsPerThread; i++)
            {
                results[basis + i] = allocator.Next(tenant, widget);
            }
        }))];

        await Task.WhenAll(tasks);

        results.Distinct().Count().ShouldBe(Threads * CallsPerThread);       // all unique
        results.Min().ShouldBe(1L);
        results.Max().ShouldBe(Threads * CallsPerThread);
    }
}
