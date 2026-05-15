namespace Granit.MultiTenancy.Endpoints.Workspaces;

/// <summary>Feature name constants for the multi-tenancy module (per ADR-057).</summary>
public static class MultiTenancyFeatures
{
    /// <summary>Tenants list — paired with <c>/multi-tenancy/tenants</c>.</summary>
    public const string Tenants = "multi-tenancy.tenants";
}
