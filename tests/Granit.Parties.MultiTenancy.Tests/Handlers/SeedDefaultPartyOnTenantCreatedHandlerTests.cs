using Granit.MultiTenancy.Events;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.MultiTenancy.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Parties.MultiTenancy.Tests.Handlers;

public sealed class SeedDefaultContactOnTenantCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToSeederWithTenantIdAndName()
    {
        IDefaultPartySeeder seeder = Substitute.For<IDefaultPartySeeder>();
        TenantCreatedEvent evt = new(Guid.NewGuid(), "ACME Inc.", "acme");
        var stub = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, evt.Name, "EUR");
        seeder.SeedForTenantAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(stub);

        await SeedDefaultPartyOnTenantCreatedHandler.HandleAsync(
            evt, seeder, TestContext.Current.CancellationToken);

        await seeder.Received(1).SeedForTenantAsync(
            evt.TenantId,
            evt.Name,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
