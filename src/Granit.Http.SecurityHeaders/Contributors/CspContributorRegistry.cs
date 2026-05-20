using System.Collections.ObjectModel;

namespace Granit.Http.SecurityHeaders.Contributors;

/// <inheritdoc cref="ICspContributorRegistry" />
internal sealed class CspContributorRegistry : ICspContributorRegistry
{
    private readonly List<ICspContributor> _contributors = [];
    private readonly System.Threading.Lock _gate = new();
    private bool _locked;
    private IReadOnlyCollection<ICspContributor>? _snapshot;

    /// <inheritdoc />
    public void Add(ICspContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        lock (_gate)
        {
            if (_locked)
            {
                throw new InvalidOperationException(
                    "The CSP contributor registry has been locked after the first " +
                    "response was composed. Register contributors during application " +
                    "configuration (e.g. inside UseGranitXxx) before the first request " +
                    "is served.");
            }

            // Idempotent on reference equality — re-registration is a no-op,
            // not a duplicate entry that would compose twice.
            if (!_contributors.Contains(contributor))
            {
                _contributors.Add(contributor);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ICspContributor> Contributors
    {
        get
        {
            lock (_gate)
            {
                _locked = true;
                return _snapshot ??= new ReadOnlyCollection<ICspContributor>([.. _contributors]);
            }
        }
    }
}
