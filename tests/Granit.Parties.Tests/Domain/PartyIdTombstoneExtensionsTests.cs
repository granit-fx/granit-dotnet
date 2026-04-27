using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests.Domain;

public sealed class PartyIdTombstoneExtensionsTests
{
    private readonly IPartyReader _reader = Substitute.For<IPartyReader>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();

    public PartyIdTombstoneExtensionsTests()
    {
        _dataFilter.Disable<IHasMergeTombstone>().Returns(Substitute.For<IDisposable>());
    }

    [Fact]
    public async Task ResolveCurrentAsync_AliveParty_ReturnsSameId()
    {
        Party alive = NewParty();
        var id = PartyId.Create(alive.Id);
        _reader.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(alive);

        PartyId resolved = await id.ResolveCurrentAsync(_reader, _dataFilter, TestContext.Current.CancellationToken);

        resolved.Value.ShouldBe(alive.Id);
    }

    [Fact]
    public async Task ResolveCurrentAsync_TombstonedParty_ReturnsSurvivorId()
    {
        Party tombstoned = NewParty();
        var survivorId = Guid.NewGuid();
        SetTombstone(tombstoned, survivorId);

        var id = PartyId.Create(tombstoned.Id);
        _reader.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(tombstoned);

        PartyId resolved = await id.ResolveCurrentAsync(_reader, _dataFilter, TestContext.Current.CancellationToken);

        resolved.Value.ShouldBe(survivorId);
    }

    [Fact]
    public async Task ResolveCurrentAsync_PartyNotFound_ReturnsSameId()
    {
        var id = PartyId.Create(Guid.NewGuid());
        _reader.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Party?)null);

        PartyId resolved = await id.ResolveCurrentAsync(_reader, _dataFilter, TestContext.Current.CancellationToken);

        resolved.Value.ShouldBe(id.Value);
    }

    [Fact]
    public async Task ResolveCurrentAsync_DisablesAndRestoresTombstoneFilter()
    {
        Party alive = NewParty();
        var id = PartyId.Create(alive.Id);
        _reader.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(alive);

        IDisposable disposable = Substitute.For<IDisposable>();
        _dataFilter.Disable<IHasMergeTombstone>().Returns(disposable);

        await id.ResolveCurrentAsync(_reader, _dataFilter, TestContext.Current.CancellationToken);

        _dataFilter.Received(1).Disable<IHasMergeTombstone>();
        disposable.Received(1).Dispose();
    }

    [Fact]
    public async Task ResolveCurrentAsync_RejectsNullPartyId() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            ((PartyId)null!).ResolveCurrentAsync(_reader, _dataFilter, default).AsTask());

    [Fact]
    public async Task ResolveCurrentAsync_RejectsNullReader() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            PartyId.Create(Guid.NewGuid()).ResolveCurrentAsync(null!, _dataFilter, default).AsTask());

    [Fact]
    public async Task ResolveCurrentAsync_RejectsNullDataFilter() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            PartyId.Create(Guid.NewGuid()).ResolveCurrentAsync(_reader, null!, default).AsTask());

    private static Party NewParty() =>
        Party.Create(Guid.NewGuid(), tenantId: null, PartyKind.Company, "Acme Corp", "EUR");

    // Reflection helper — MergedIntoId / MergedAt have private setters; the merge
    // orchestrator (#1284) sets them inside the merge transaction. For tests we mutate
    // them directly to construct a tombstoned aggregate.
    private static void SetTombstone(Party party, Guid survivorId)
    {
        typeof(Party).GetProperty(nameof(Party.MergedIntoId))!
            .SetValue(party, (Guid?)survivorId);
        typeof(Party).GetProperty(nameof(Party.MergedAt))!
            .SetValue(party, (DateTimeOffset?)DateTimeOffset.UtcNow);
    }
}
