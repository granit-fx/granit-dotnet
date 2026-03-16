using Granit.BackgroundJobs.Domain;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Thread-safe, additive collection of <see cref="RecurringJobRegistration"/> descriptors.
/// Registered as a singleton so that both <see cref="BackgroundJobsSeedService"/> (which reads)
/// and <c>AddGranitBackgroundJobAssemblies</c> (which writes) share the same instance.
/// </summary>
internal sealed class RecurringJobRegistrationCollection
{
    private readonly List<RecurringJobRegistration> _registrations = [];
    private readonly Lock _lock = new();

    internal void AddRange(IEnumerable<RecurringJobRegistration> registrations)
    {
        lock (_lock)
        {
            foreach (RecurringJobRegistration? registration in registrations
                .Where(r => !_registrations.Exists(existing => existing.JobName == r.JobName)))
            {
                _registrations.Add(registration);
            }
        }
    }

    internal IReadOnlyList<RecurringJobRegistration> ToList()
    {
        lock (_lock)
        {
            return [.. _registrations];
        }
    }
}
