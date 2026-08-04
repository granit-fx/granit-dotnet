using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Testing.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Suites;

/// <summary>
/// Proves the interceptor pipeline (audit → concurrency stamp → soft delete) against a real
/// database, including the optimistic-concurrency 409 path the InMemory provider cannot test.
/// </summary>
public abstract class InterceptorPipelineConformanceSuite(IRelationalConformanceFixture fixture)
{
    [Fact]
    public async Task Insert_stamps_audit_fields_id_tenant_and_concurrency_stamp()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        var tenant = Guid.CreateVersion7();
        harness.CurrentTenant.Id = tenant;
        harness.CurrentUser.UserId = "conformance-author";

        await using ConformanceDbContext db = await harness.CreateContextAsync();
        ConformanceOrder order = new() { Label = "audit" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        order.Id.ShouldNotBe(Guid.Empty);
        order.CreatedAt.ShouldBe(harness.Clock.Now);
        order.CreatedBy.ShouldBe("conformance-author");
        order.TenantId.ShouldBe(tenant);
        order.ConcurrencyStamp.Length.ShouldBe(36);
        order.ModifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Update_stamps_modification_fields_and_rotates_the_stamp()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();

        await using ConformanceDbContext db = await harness.CreateContextAsync();
        ConformanceOrder order = new() { Label = "modify" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        string initialStamp = order.ConcurrencyStamp;

        harness.CurrentUser.UserId = "conformance-editor";
        order.Status = ConformanceOrderStatus.Approved;
        await db.SaveChangesAsync();

        order.ModifiedAt.ShouldBe(harness.Clock.Now);
        order.ModifiedBy.ShouldBe("conformance-editor");
        order.ConcurrencyStamp.ShouldNotBe(initialStamp,
            $"{fixture.ProviderName}: the concurrency stamp must rotate on every save");
    }

    [Fact]
    public async Task Stale_concurrency_stamp_raises_DbUpdateConcurrencyException()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();

        Guid id;
        string staleStamp;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            ConformanceOrder order = new() { Label = "concurrency" };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            id = order.Id;
            staleStamp = order.ConcurrencyStamp;

            // A second writer wins the race: the stored stamp rotates.
            order.Status = ConformanceOrderStatus.Approved;
            await db.SaveChangesAsync();
        }

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            ConformanceOrder detached = new() { Id = id, Label = "concurrency", Status = ConformanceOrderStatus.Rejected };
            db.Orders.Update(detached);
            db.SetConcurrencyStampOriginalValue(detached, staleStamp);

            await Should.ThrowAsync<DbUpdateConcurrencyException>(
                async () => await db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Cross_tenant_insert_is_blocked_fail_closed()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();

        await using ConformanceDbContext db = await harness.CreateContextAsync();
        db.Orders.Add(new ConformanceOrder { Label = "cross", TenantId = Guid.CreateVersion7() });

        (await Should.ThrowAsync<InvalidOperationException>(async () => await db.SaveChangesAsync()))
            .Message.ShouldContain("Cross-tenant write blocked");
    }
}
