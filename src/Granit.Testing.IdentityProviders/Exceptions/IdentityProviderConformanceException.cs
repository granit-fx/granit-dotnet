namespace Granit.Testing.IdentityProviders.Exceptions;

/// <summary>
/// Thrown by the identity-provider conformance checks when a provider violates a contract that
/// every Granit identity provider must uphold. A test wrapping the check surfaces this as a failure.
/// </summary>
public sealed class IdentityProviderConformanceException(string message) : Exception(message);
