// CA1711: the "Exception" suffix on an interface is intentional.
// This convention makes it immediately clear that the interface marks exceptions
// whose messages are safe to expose to end users. Renaming to "IUserFriendlyError" would
// break the semantic clarity of the pattern (inspired by ABP's naming convention).
#pragma warning disable CA1711

namespace Granit.Exceptions;

/// <summary>
/// Marker interface indicating that the exception message is safe to expose to end users.
/// <para>
/// <b>ISO 27001 / Security rule:</b> only exceptions explicitly implementing this interface
/// may have their message included in the HTTP response body. Any exception that does
/// NOT implement <see cref="IUserFriendlyException"/> will have its message replaced
/// by a generic, non-sensitive title in production environments.
/// </para>
/// <para>
/// <b>Never</b> use this interface on exceptions whose message may contain PHI (patient data),
/// internal paths, SQL fragments, or any other sensitive information.
/// </para>
/// </summary>
public interface IUserFriendlyException;

#pragma warning restore CA1711
