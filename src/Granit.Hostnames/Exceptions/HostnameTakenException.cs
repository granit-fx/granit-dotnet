using Granit.Exceptions;

namespace Granit.Hostnames.Exceptions;

/// <summary>
/// Thrown when a hostname registration is rejected because the host value is already taken.
/// Maps to <c>409 Conflict</c>.
/// </summary>
public sealed class HostnameTakenException : ConflictException
{
    /// <summary>The host value that is already registered.</summary>
    public string Host { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HostnameTakenException"/>.
    /// </summary>
    /// <param name="host">The hostname that is already registered.</param>
    public HostnameTakenException(string host)
        : base("Hostnames:HostnameTaken", $"The hostname '{host}' is already registered.")
    {
        Host = host;
    }
}
