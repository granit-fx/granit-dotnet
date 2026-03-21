using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeCurrentTenantAdditionalTests
{
    [Fact]
    public void Setting_Name_Updates_Value()
    {
        FakeCurrentTenant tenant = new();
        const string name = "Acme Corp";

        tenant.Name = name;

        tenant.Name.ShouldBe(name);
    }

    [Fact]
    public void Setting_IsAvailable_Directly_Updates_Value()
    {
        FakeCurrentTenant tenant = new();

        tenant.IsAvailable = true;

        tenant.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Setting_IsAvailable_To_False_After_True()
    {
        FakeCurrentTenant tenant = new();
        tenant.IsAvailable = true;

        tenant.IsAvailable = false;

        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Change_Without_Name_Sets_Null_Name()
    {
        FakeCurrentTenant tenant = new();
        var tenantId = Guid.NewGuid();

        using (tenant.Change(tenantId))
        {
            tenant.Id.ShouldBe(tenantId);
            tenant.Name.ShouldBeNull();
            tenant.IsAvailable.ShouldBeTrue();
        }
    }

    [Fact]
    public void Change_Dispose_Is_Idempotent()
    {
        FakeCurrentTenant tenant = new();
        var originalId = Guid.NewGuid();
        var tempId = Guid.NewGuid();
        tenant.Id = originalId;

        IDisposable scope = tenant.Change(tempId);
        scope.Dispose();
        scope.Dispose(); // second dispose should be harmless

        tenant.Id.ShouldBe(originalId);
    }

    [Fact]
    public void Change_Restores_Null_State_When_No_Previous()
    {
        FakeCurrentTenant tenant = new();
        var tempId = Guid.NewGuid();

        using (tenant.Change(tempId, "Temp"))
        {
            tenant.Id.ShouldBe(tempId);
            tenant.Name.ShouldBe("Temp");
        }

        tenant.IsAvailable.ShouldBeFalse();
        tenant.Id.ShouldBeNull();
        tenant.Name.ShouldBeNull();
    }

    [Fact]
    public void Nested_Change_Scopes_Restore_Correctly()
    {
        FakeCurrentTenant tenant = new();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        tenant.Id = id1;

        using (tenant.Change(id2, "Level2"))
        {
            tenant.Id.ShouldBe(id2);

            using (tenant.Change(id3, "Level3"))
            {
                tenant.Id.ShouldBe(id3);
                tenant.Name.ShouldBe("Level3");
            }

            tenant.Id.ShouldBe(id2);
            tenant.Name.ShouldBe("Level2");
        }

        tenant.Id.ShouldBe(id1);
    }

    [Fact]
    public void Setting_Name_On_Fresh_Instance_Creates_State()
    {
        FakeCurrentTenant tenant = new();

        tenant.Name = "Fresh";

        tenant.Name.ShouldBe("Fresh");
    }
}
