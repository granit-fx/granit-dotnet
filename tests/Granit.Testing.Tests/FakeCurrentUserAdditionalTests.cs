using Granit.Testing.Fakes;
using Granit.Users;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeCurrentUserAdditionalTests
{
    [Fact]
    public void Setting_Email_Updates_Value()
    {
        FakeCurrentUser user = new();
        const string email = "custom@example.com";

        user.Email = email;

        user.Email.ShouldBe(email);
    }

    [Fact]
    public void Setting_FirstName_Updates_Value()
    {
        FakeCurrentUser user = new();
        const string firstName = "Alice";

        user.FirstName = firstName;

        user.FirstName.ShouldBe(firstName);
    }

    [Fact]
    public void Setting_LastName_Updates_Value()
    {
        FakeCurrentUser user = new();
        const string lastName = "Smith";

        user.LastName = lastName;

        user.LastName.ShouldBe(lastName);
    }

    [Fact]
    public void Setting_ApiKeyId_Updates_Value()
    {
        FakeCurrentUser user = new();
        var apiKeyId = Guid.NewGuid();

        user.ApiKeyId = apiKeyId;

        user.ApiKeyId.ShouldBe(apiKeyId);
    }

    [Fact]
    public void Setting_IsAuthenticated_To_False_Updates_Value()
    {
        FakeCurrentUser user = new();

        user.IsAuthenticated = false;

        user.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void Setting_IsMachine_To_False_Sets_ActorKind_User()
    {
        FakeCurrentUser user = new();
        user.IsMachine = true;
        user.ActorKind.ShouldBe(ActorKind.ExternalSystem);

        user.IsMachine = false;

        user.ActorKind.ShouldBe(ActorKind.User);
        user.IsMachine.ShouldBeFalse();
    }

    [Fact]
    public void Setting_UserName_Updates_Value()
    {
        FakeCurrentUser user = new();
        const string name = "CustomUser";

        user.UserName = name;

        user.UserName.ShouldBe(name);
    }

    [Fact]
    public void Setting_UserId_Updates_Value()
    {
        FakeCurrentUser user = new();
        const string id = "custom-id-42";

        user.UserId = id;

        user.UserId.ShouldBe(id);
    }

    [Fact]
    public void GetRoles_Returns_All_Added_Roles()
    {
        FakeCurrentUser user = new();

        user.AddRole("Admin");
        user.AddRole("Editor");
        user.AddRole("Viewer");

        IReadOnlyList<string> roles = user.GetRoles();
        roles.Count.ShouldBe(3);
        roles.ShouldContain("Admin");
        roles.ShouldContain("Editor");
        roles.ShouldContain("Viewer");
    }

    [Fact]
    public void IsInRole_Is_Case_Insensitive()
    {
        FakeCurrentUser user = new();
        user.AddRole("SuperAdmin");

        user.IsInRole("superadmin").ShouldBeTrue();
        user.IsInRole("SUPERADMIN").ShouldBeTrue();
        user.IsInRole("SuperAdmin").ShouldBeTrue();
    }

    [Fact]
    public void IsInRole_Returns_False_For_Missing_Role()
    {
        FakeCurrentUser user = new();

        user.IsInRole("NonExistent").ShouldBeFalse();
    }

    [Fact]
    public void ActorKind_System_Makes_IsMachine_True()
    {
        FakeCurrentUser user = new();

        user.ActorKind = ActorKind.System;

        user.IsMachine.ShouldBeTrue();
    }

    [Fact]
    public void Setting_Properties_On_Fresh_Instance_Creates_State()
    {
        FakeCurrentUser user = new();

        // These should all work without errors on a fresh instance
        user.Email = "new@example.com";
        user.FirstName = "New";
        user.LastName = "Person";
        user.ApiKeyId = Guid.NewGuid();

        user.Email.ShouldBe("new@example.com");
        user.FirstName.ShouldBe("New");
        user.LastName.ShouldBe("Person");
        user.ApiKeyId.ShouldNotBeNull();
    }
}
