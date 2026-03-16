namespace Granit.Persistence;

/// <summary>
/// Named query filter keys registered by
/// <see cref="Extensions.ModelBuilderExtensions.ApplyGranitConventions"/>.
/// </summary>
/// <remarks>
/// Use with <c>queryable.IgnoreQueryFilters([GranitFilterNames.SoftDelete])</c>
/// to bypass a specific filter on a single query without disabling it globally.
/// For service-level bypass (all queries in the current async flow), use
/// <c>IDataFilter.Disable&lt;ISoftDeletable&gt;()</c> instead.
/// </remarks>
public static class GranitFilterNames
{
    /// <summary>Filter key for <see cref="Core.Domain.ISoftDeletable"/> — excludes logically deleted entities.</summary>
    public const string SoftDelete = "SoftDelete";

    /// <summary>Filter key for <see cref="Core.Domain.IActive"/> — excludes inactive entities.</summary>
    public const string Active = "Active";

    /// <summary>Filter key for <see cref="Core.Domain.IProcessingRestrictable"/> — excludes GDPR-restricted entities.</summary>
    public const string ProcessingRestrictable = "ProcessingRestrictable";

    /// <summary>Filter key for <see cref="Core.MultiTenancy.IMultiTenant"/> — restricts queries to the current tenant.</summary>
    public const string MultiTenant = "MultiTenant";

    /// <summary>Filter key for <see cref="Core.Domain.IPublishable"/> — excludes unpublished entities.</summary>
    public const string Publishable = "Publishable";
}
