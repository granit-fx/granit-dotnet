using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeCurrentTenantTests
{
    [Fact]
    public void Default_State_Is_Unavailable()
    {
        FakeCurrentTenant tenant = new();

        tenant.IsAvailable.ShouldBeFalse();
        tenant.Id.ShouldBeNull();
        tenant.Name.ShouldBeNull();
    }

    [Fact]
    public void Setting_Id_Makes_Tenant_Available()
    {
        FakeCurrentTenant tenant = new();
        var id = Guid.NewGuid();

        tenant.Id = id;

        tenant.IsAvailable.ShouldBeTrue();
        tenant.Id.ShouldBe(id);
    }

    [Fact]
    public void Setting_Id_To_Null_Makes_Tenant_Unavailable()
    {
        FakeCurrentTenant tenant = new();
        tenant.Id = Guid.NewGuid();

        tenant.Id = null;

        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Change_Sets_New_Tenant_And_Restores_On_Dispose()
    {
        FakeCurrentTenant tenant = new();
        var originalId = Guid.NewGuid();
        var tempId = Guid.NewGuid();
        tenant.Id = originalId;
        tenant.Name = "Original";

        using (tenant.Change(tempId, "Temp"))
        {
            tenant.IsAvailable.ShouldBeTrue();
            tenant.Id.ShouldBe(tempId);
            tenant.Name.ShouldBe("Temp");
        }

        tenant.IsAvailable.ShouldBeTrue();
        tenant.Id.ShouldBe(originalId);
        tenant.Name.ShouldBe("Original");
    }

    [Fact]
    public void Change_To_Null_Deactivates_Tenant()
    {
        FakeCurrentTenant tenant = new();
        tenant.Id = Guid.NewGuid();

        using (tenant.Change(null))
        {
            tenant.IsAvailable.ShouldBeFalse();
            tenant.Id.ShouldBeNull();
        }

        tenant.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task AsyncLocal_Isolates_State_Across_Tasks()
    {
        FakeCurrentTenant tenant = new();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

#pragma warning disable xUnit1051
        var task1 = Task.Run(async () =>
        {
            tenant.Id = id1;
            tenant.Name = "Tenant1";
            await Task.Delay(50);
            tenant.Id.ShouldBe(id1);
            tenant.Name.ShouldBe("Tenant1");
        });

        var task2 = Task.Run(async () =>
        {
            tenant.Id = id2;
            tenant.Name = "Tenant2";
            await Task.Delay(50);
            tenant.Id.ShouldBe(id2);
            tenant.Name.ShouldBe("Tenant2");
        });

        await Task.WhenAll(task1, task2);
#pragma warning restore xUnit1051
    }
}
