namespace Granit.Hostnames.Domain;

/// <summary>Category of DNS conflict detected during verification.</summary>
public enum DnsConflictType
{
    /// <summary>An unexpected A record is present (e.g. pointing at a different IP).</summary>
    UnexpectedA = 0,

    /// <summary>An unexpected AAAA record is present.</summary>
    UnexpectedAaaa = 1,

    /// <summary>A CNAME record points to the wrong target.</summary>
    DivergentCname = 2,

    /// <summary>The expected CNAME record is missing.</summary>
    MissingCname = 3,

    /// <summary>The expected TXT verification challenge record is absent or wrong.</summary>
    MissingTxt = 4,

    /// <summary>DNS resolution failed or timed out.</summary>
    ResolutionFailure = 5,
}
