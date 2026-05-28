using Granit.Presence.Abstractions;
using Granit.Presence.Internal;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Internal;

public sealed class AllowAllResourcePresenceVisibilityPolicyTests
{
    private static readonly ResourceRef SampleResource = new("document", "abc-123");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CanReadRoomAsync_returns_true_always()
    {
        var policy = new AllowAllResourcePresenceVisibilityPolicy();

        bool ok = await policy.CanReadRoomAsync(Guid.NewGuid(), SampleResource, Ct);

        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task FilterVisibleParticipantsAsync_returns_unchanged_set()
    {
        var policy = new AllowAllResourcePresenceVisibilityPolicy();
        Guid[] participants = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        IReadOnlySet<Guid> visible = await policy.FilterVisibleParticipantsAsync(
            Guid.NewGuid(), SampleResource, participants, Ct);

        visible.Count.ShouldBe(participants.Length);
        foreach (Guid id in participants)
        {
            visible.ShouldContain(id);
        }
    }
}
