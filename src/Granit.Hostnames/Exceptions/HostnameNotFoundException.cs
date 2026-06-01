using Granit.Exceptions;

namespace Granit.Hostnames.Exceptions;

/// <summary>
/// Thrown when a managed hostname is not found for the given identifier.
/// Maps to <c>404 Not Found</c>.
/// </summary>
public sealed class HostnameNotFoundException : NotFoundException
{
    /// <summary>The identifier that was used in the lookup.</summary>
    public Guid Id { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HostnameNotFoundException"/>.
    /// </summary>
    /// <param name="id">Identifier used in the lookup.</param>
    public HostnameNotFoundException(Guid id)
        : base($"No managed hostname with id '{id}' was found.")
    {
        Id = id;
    }
}
