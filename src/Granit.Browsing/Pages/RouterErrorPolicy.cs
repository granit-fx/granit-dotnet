namespace Granit.Browsing.Pages;

/// <summary>Behaviour when a user-registered request-router handler throws.</summary>
public enum RouterErrorPolicy
{
    /// <summary>Abort the request — fail-closed default.</summary>
    AbortOnError,

    /// <summary>Skip the throwing handler and continue down the chain.</summary>
    ContinueOnError,
}
