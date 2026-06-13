using System.Collections.Concurrent;

namespace Granit.Identity.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="IUserBehavioralProfileStore"/> registered by default. Suitable
/// for development and tests; replaced by <c>Granit.Identity.EntityFrameworkCore</c> for durable production use
/// (the in-memory profile resets on restart, so the false-positive reduction would not survive one).
/// </summary>
internal sealed class MemoryUserBehavioralProfileStore : IUserBehavioralProfileStore
{
    private sealed class Entry
    {
        public int Count { get; set; }
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
    }

    private readonly ConcurrentDictionary<(string UserId, BehavioralObservationKind Kind, string Value), Entry> _entries =
        new();
    private readonly Lock _gate = new();

    public Task<UserBehavioralProfile> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        List<BehavioralObservation> observations;
        lock (_gate)
        {
            observations =
            [
                .. _entries
                    .Where(kvp => kvp.Key.UserId == userId)
                    .Select(kvp => new BehavioralObservation(
                        kvp.Key.Kind, kvp.Key.Value, kvp.Value.Count, kvp.Value.FirstSeenAt, kvp.Value.LastSeenAt)),
            ];
        }

        return Task.FromResult(observations.Count == 0 ? UserBehavioralProfile.Empty : new UserBehavioralProfile(observations));
    }

    public Task RecordObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Record(userId, BehavioralObservationKind.Country, country, observedAt);
            Record(userId, BehavioralObservationKind.DeviceFamily, deviceFamily, observedAt);
            Record(userId, BehavioralObservationKind.CoarseLocation, coarseLocation, observedAt);
        }

        return Task.CompletedTask;
    }

    private void Record(string userId, BehavioralObservationKind kind, string? value, DateTimeOffset observedAt)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (_entries.TryGetValue((userId, kind, value), out Entry? entry))
        {
            entry.Count++;
            entry.LastSeenAt = observedAt;
        }
        else
        {
            _entries[(userId, kind, value)] = new Entry { Count = 1, FirstSeenAt = observedAt, LastSeenAt = observedAt };
        }
    }
}
