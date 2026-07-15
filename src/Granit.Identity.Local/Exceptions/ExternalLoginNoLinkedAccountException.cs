namespace Granit.Identity.Local.Exceptions;

/// <summary>
/// Thrown during an external-login callback when the authenticated external identity
/// matches no local account and auto-registration is disabled — a deliberate 403 path.
/// </summary>
/// <remarks>
/// Replaces a message-string check (<c>ex.Message.Contains("not found")</c>) at the
/// endpoint, which would silently stop matching under a localized
/// <c>IdentityErrorDescriber</c>. Derives from <see cref="InvalidOperationException"/>
/// for back-compat with existing generic catch blocks.
/// </remarks>
public sealed class ExternalLoginNoLinkedAccountException()
    : InvalidOperationException("No account is linked to this external login.");
