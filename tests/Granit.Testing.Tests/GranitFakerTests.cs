using Bogus;
using Granit.Testing.Fakes;
using Granit.Testing.Generators;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitFakerTests
{
    [Fact]
    public void CurrentUser_Generates_Valid_User()
    {
        FakeCurrentUser user = GranitFaker.CurrentUser().Generate();

        user.UserId.ShouldNotBeNullOrWhiteSpace();
        user.UserName.ShouldNotBeNullOrWhiteSpace();
        user.Email.ShouldNotBeNullOrWhiteSpace();
        user.FirstName.ShouldNotBeNullOrWhiteSpace();
        user.LastName.ShouldNotBeNullOrWhiteSpace();
        user.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public void CurrentUser_Generates_Unique_Users()
    {
        Faker<FakeCurrentUser> faker = GranitFaker.CurrentUser();

        FakeCurrentUser user1 = faker.Generate();
        FakeCurrentUser user2 = faker.Generate();

        user1.UserId.ShouldNotBe(user2.UserId);
    }

    [Fact]
    public void Tenant_Generates_Valid_Tenant()
    {
        FakeCurrentTenant tenant = GranitFaker.Tenant().Generate();

        tenant.Id.ShouldNotBeNull();
        tenant.Id.ShouldNotBe(Guid.Empty);
        tenant.Name.ShouldNotBeNullOrWhiteSpace();
        tenant.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Tenant_Generates_Unique_Tenants()
    {
        Faker<FakeCurrentTenant> faker = GranitFaker.Tenant();

        FakeCurrentTenant t1 = faker.Generate();
        FakeCurrentTenant t2 = faker.Generate();

        t1.Id.ShouldNotBe(t2.Id);
    }
}
