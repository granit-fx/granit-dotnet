using Granit.Contacts;
using Granit.Contacts.Domain;
using Granit.Contacts.MultiTenancy.Handlers;
using Granit.MultiTenancy.Events;
using NSubstitute;
using Xunit;

namespace Granit.Contacts.MultiTenancy.Tests.Handlers;

public sealed class SeedDefaultContactOnTenantCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToSeederWithTenantIdAndName()
    {
        IDefaultContactSeeder seeder = Substitute.For<IDefaultContactSeeder>();
        TenantCreatedEvent evt = new(Guid.NewGuid(), "ACME Inc.", "acme");
        var stub = Contact.Create(
            Guid.NewGuid(), null, ContactKind.Company, evt.Name, "EUR");
        seeder.SeedForTenantAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(stub);

        await SeedDefaultContactOnTenantCreatedHandler.HandleAsync(
            evt, seeder, TestContext.Current.CancellationToken);

        await seeder.Received(1).SeedForTenantAsync(
            evt.TenantId,
            evt.Name,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
