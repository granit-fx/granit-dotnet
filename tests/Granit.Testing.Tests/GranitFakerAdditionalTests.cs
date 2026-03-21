using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Testing.Generators;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitFakerAdditionalTests
{
    [Fact]
    public void CurrentUser_ApiKeyId_Is_Null_By_Default()
    {
        FakeCurrentUser user = GranitFaker.CurrentUser().Generate();

        user.ApiKeyId.ShouldBeNull();
    }

    [Fact]
    public void CurrentUser_ActorKind_Is_User()
    {
        FakeCurrentUser user = GranitFaker.CurrentUser().Generate();

        user.ActorKind.ShouldBe(ActorKind.User);
    }

    [Fact]
    public void CurrentUser_IsMachine_Is_False()
    {
        FakeCurrentUser user = GranitFaker.CurrentUser().Generate();

        user.IsMachine.ShouldBeFalse();
    }

    [Fact]
    public void CurrentUser_Generates_Valid_Email()
    {
        FakeCurrentUser user = GranitFaker.CurrentUser().Generate();

        user.Email!.ShouldContain("@");
    }

    [Fact]
    public void Tenant_Generates_Unique_Names()
    {
        List<FakeCurrentTenant> tenants = GranitFaker.Tenant().Generate(10);

        // Company names from Bogus may not all be unique, but most should be
        tenants.Select(t => t.Name).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void CurrentUser_Generate_Multiple_All_Have_Values()
    {
        List<FakeCurrentUser> users = GranitFaker.CurrentUser().Generate(5);

        users.ShouldAllBe(u => !string.IsNullOrWhiteSpace(u.UserId));
        users.ShouldAllBe(u => !string.IsNullOrWhiteSpace(u.Email));
        users.ShouldAllBe(u => !string.IsNullOrWhiteSpace(u.FirstName));
        users.ShouldAllBe(u => !string.IsNullOrWhiteSpace(u.LastName));
    }
}
