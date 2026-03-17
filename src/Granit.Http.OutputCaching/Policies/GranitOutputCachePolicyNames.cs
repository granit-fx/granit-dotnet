namespace Granit.Http.OutputCaching.Policies;

/// <summary>
/// Named output cache policy constants for use with <c>.CacheOutput(policyName)</c>
/// or <c>[OutputCache(PolicyName = "...")]</c>.
/// </summary>
public static class GranitOutputCachePolicyNames
{
    /// <summary>
    /// Default policy: GDPR-compliant, tenant-aware, with configured expiration and VaryByQuery.
    /// </summary>
    public const string Default = "GranitDefault";

    /// <summary>
    /// Explicitly disables caching for an endpoint.
    /// </summary>
    public const string NoCache = "GranitNoCache";
}
