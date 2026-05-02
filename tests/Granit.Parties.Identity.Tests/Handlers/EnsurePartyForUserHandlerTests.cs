using Granit.Guids;
using Granit.Identity.Events;
using Granit.Parties.Domain;
using Granit.Parties.Identity.Handlers;
using Granit.Parties.Identity.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Identity.Tests.Handlers;

/// <summary>
/// Locks the contract of <see cref="EnsurePartyForUserHandler"/> per
/// ADR-051 B-step 5.
/// </summary>
public sealed class EnsurePartyForUserHandlerTests
{
    private readonly IPartyReader _reader = Substitute.For<IPartyReader>();
    private readonly IPartyWriter _writer = Substitute.For<IPartyWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IOptions<GranitPartiesIdentityOptions> _options =
        Microsoft.Extensions.Options.Options.Create(new GranitPartiesIdentityOptions { DefaultCurrency = "EUR" });

    [Fact]
    public async Task HandleAsync_CreatesPartyOfKindIndividual_WithDisplayNameAndDefaultCurrency()
    {
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _guidGenerator.Create().Returns(partyId);
        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);

        UserCreatedEto evt = new(
            UserId: userId,
            DisplayName: "Alice Doe",
            Email: "alice@example.com",
            FirstName: "Alice",
            LastName: "Doe",
            PhoneNumber: null,
            TenantId: tenantId);

        await EnsurePartyForUserHandler.HandleAsync(evt, _reader, _writer, _guidGenerator, _options, TestContext.Current.CancellationToken);

        await _writer.Received(1).AddAsync(
            Arg.Is<Party>(p =>
                p.Id == partyId
                && p.Kind == PartyKind.Individual
                && p.Name == "Alice Doe"
                && p.UserId == userId
                && p.TenantId == tenantId
                && p.DefaultCurrency == "EUR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HostScopedUser_CreatesHostScopedParty()
    {
        var userId = Guid.NewGuid();
        _guidGenerator.Create().Returns(Guid.NewGuid());
        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);

        UserCreatedEto evt = new(
            UserId: userId,
            DisplayName: "Platform Admin",
            Email: "platform@example.com",
            FirstName: null,
            LastName: null,
            PhoneNumber: null,
            TenantId: null);

        await EnsurePartyForUserHandler.HandleAsync(evt, _reader, _writer, _guidGenerator, _options, TestContext.Current.CancellationToken);

        await _writer.Received(1).AddAsync(
            Arg.Is<Party>(p => p.TenantId == null && p.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_WhenPartyAlreadyLinkedToUser()
    {
        // At-least-once Wolverine delivery: the same UserCreatedEto
        // may run twice. The handler must not double-insert — keying
        // on Party.UserId is the dedup point (no DB-level unique).
        var userId = Guid.NewGuid();
        var existing = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Individual,
            name: "Existing Party",
            defaultCurrency: "EUR");
        existing.LinkToUser(userId);

        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existing);

        UserCreatedEto evt = new(
            UserId: userId,
            DisplayName: "Alice",
            Email: "alice@example.com",
            FirstName: null,
            LastName: null,
            PhoneNumber: null,
            TenantId: null);

        await EnsurePartyForUserHandler.HandleAsync(evt, _reader, _writer, _guidGenerator, _options, TestContext.Current.CancellationToken);

        await _writer.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
        _guidGenerator.DidNotReceive().Create();
    }

    [Fact]
    public async Task HandleAsync_HonoursConfiguredDefaultCurrency()
    {
        var userId = Guid.NewGuid();
        _guidGenerator.Create().Returns(Guid.NewGuid());
        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);
        IOptions<GranitPartiesIdentityOptions> usdOptions =
            Microsoft.Extensions.Options.Options.Create(new GranitPartiesIdentityOptions { DefaultCurrency = "USD" });

        UserCreatedEto evt = new(
            UserId: userId,
            DisplayName: "Bob",
            Email: "bob@example.com",
            FirstName: null, LastName: null, PhoneNumber: null,
            TenantId: null);

        await EnsurePartyForUserHandler.HandleAsync(evt, _reader, _writer, _guidGenerator, usdOptions, TestContext.Current.CancellationToken);

        await _writer.Received(1).AddAsync(
            Arg.Is<Party>(p => p.DefaultCurrency == "USD"),
            Arg.Any<CancellationToken>());
    }
}
