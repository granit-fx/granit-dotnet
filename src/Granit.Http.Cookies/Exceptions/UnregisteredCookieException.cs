using Granit.Exceptions;

namespace Granit.Http.Cookies.Exceptions;

/// <summary>
/// Thrown when attempting to set or delete a cookie that is not registered in the <see cref="ICookieRegistry"/>.
/// Fail-Fast behavior enforcing the Strict Registry Pattern.
/// </summary>
public sealed class UnregisteredCookieException : BusinessException
{
    /// <summary>The name of the unregistered cookie.</summary>
    public string CookieName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="UnregisteredCookieException"/>.
    /// </summary>
    public UnregisteredCookieException(string cookieName)
        : base("Cookies:Unregistered", $"Cookie '{cookieName}' is not registered. All cookies must be declared at startup via the cookie registry.")
    {
        CookieName = cookieName;
    }
}
