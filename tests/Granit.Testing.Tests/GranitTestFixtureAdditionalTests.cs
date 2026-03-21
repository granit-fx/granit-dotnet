using Granit.Core.Modularity;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Timing;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitTestFixtureAdditionalTests
{
    [Fact]
    public void Dispose_Does_Not_Throw_Before_Build()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();

        Should.NotThrow(() => fixture.Dispose());
    }

    [Fact]
    public async Task Dispose_Does_Not_Throw_After_Build()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();
        await fixture.BuildAsync();

        Should.NotThrow(() => fixture.Dispose());
    }

    [Fact]
    public async Task DisposeAsync_Does_Not_Throw_Before_Build()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();

        await Should.NotThrowAsync(async () => await fixture.DisposeAsync());
    }

    [Fact]
    public async Task DisposeAsync_Stops_And_Disposes_Host()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();
        await fixture.BuildAsync();

        await fixture.DisposeAsync();

        Should.Throw<InvalidOperationException>(() => _ = fixture.ServiceProvider);
    }

    [Fact]
    public async Task Multiple_Dispose_Is_Safe()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();
        await fixture.BuildAsync();

        fixture.Dispose();
        Should.NotThrow(() => fixture.Dispose());
    }

    [Fact]
    public void Fixture_Exposes_Fakes()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();

        fixture.Tenant.ShouldNotBeNull();
        fixture.Tenant.ShouldBeOfType<FakeCurrentTenant>();
        fixture.User.ShouldNotBeNull();
        fixture.User.ShouldBeOfType<FakeCurrentUser>();
        fixture.Clock.ShouldNotBeNull();
        fixture.Clock.ShouldBeOfType<FakeClock>();
        fixture.GuidGenerator.ShouldNotBeNull();
        fixture.GuidGenerator.ShouldBeOfType<FakeGuidGenerator>();
    }

    [Fact]
    public async Task GetService_Returns_Registered_Service()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();
        await fixture.BuildAsync();

        IClock? clock = fixture.GetService<IClock>();

        clock.ShouldNotBeNull();
        clock.ShouldBeSameAs(fixture.Clock);

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task Fakes_Can_Be_Configured_Before_Build()
    {
        GranitTestFixture<GranitTestingModule> fixture = new();
        var tenantId = Guid.NewGuid();
        fixture.Tenant.Id = tenantId;
        fixture.User.UserId = "custom-user";
        fixture.Clock.Now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await fixture.BuildAsync();

        ICurrentTenant resolvedTenant = fixture.GetRequiredService<ICurrentTenant>();
        resolvedTenant.Id.ShouldBe(tenantId);

        ICurrentUserService resolvedUser = fixture.GetRequiredService<ICurrentUserService>();
        resolvedUser.UserId.ShouldBe("custom-user");

        IClock resolvedClock = fixture.GetRequiredService<IClock>();
        resolvedClock.Now.Year.ShouldBe(2030);

        await fixture.DisposeAsync();
    }
}
