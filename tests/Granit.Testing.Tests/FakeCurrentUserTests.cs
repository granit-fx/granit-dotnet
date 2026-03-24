using Granit.Testing.Fakes;
using Granit.Users;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeCurrentUserTests
{
    [Fact]
    public void Default_State_Has_Sensible_Values()
    {
        FakeCurrentUser user = new();

        user.UserId.ShouldBe("test-user-001");
        user.UserName.ShouldBe("Test User");
        user.Email.ShouldBe("test@example.com");
        user.FirstName.ShouldBe("Test");
        user.LastName.ShouldBe("User");
        user.IsAuthenticated.ShouldBeTrue();
        user.ActorKind.ShouldBe(ActorKind.User);
        user.IsMachine.ShouldBeFalse();
        user.ApiKeyId.ShouldBeNull();
        user.GetRoles().ShouldBeEmpty();
    }

    [Fact]
    public void AddRole_And_IsInRole_Work()
    {
        FakeCurrentUser user = new();

        user.AddRole("Admin");
        user.AddRole("Editor");

        user.IsInRole("Admin").ShouldBeTrue();
        user.IsInRole("admin").ShouldBeTrue(); // case-insensitive
        user.IsInRole("Editor").ShouldBeTrue();
        user.IsInRole("Viewer").ShouldBeFalse();
        user.GetRoles().Count.ShouldBe(2);
    }

    [Fact]
    public void ClearRoles_Removes_All_Roles()
    {
        FakeCurrentUser user = new();
        user.AddRole("Admin");

        user.ClearRoles();

        user.GetRoles().ShouldBeEmpty();
    }

    [Fact]
    public void Setting_IsMachine_Updates_ActorKind()
    {
        FakeCurrentUser user = new();

        user.IsMachine = true;

        user.ActorKind.ShouldBe(ActorKind.ExternalSystem);
        user.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public void Setting_ActorKind_To_System_Makes_IsMachine_True()
    {
        FakeCurrentUser user = new();

        user.ActorKind = ActorKind.System;

        user.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public async Task AsyncLocal_Isolates_State_Across_Tasks()
    {
        FakeCurrentUser user = new();

#pragma warning disable xUnit1051
        var task1 = Task.Run(async () =>
        {
            user.UserId = "user-A";
            await Task.Delay(50);
            user.UserId.ShouldBe("user-A");
        });

        var task2 = Task.Run(async () =>
        {
            user.UserId = "user-B";
            await Task.Delay(50);
            user.UserId.ShouldBe("user-B");
        });

        await Task.WhenAll(task1, task2);
#pragma warning restore xUnit1051
    }
}
