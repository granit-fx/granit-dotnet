namespace Granit.ReferenceData.Domain;

/// <summary>
/// Concrete, non-abstract reference data entity for dynamically registered types.
/// Used by the simplified <c>AddReferenceData&lt;TDbContext&gt;()</c> API when no
/// strongly-typed subclass is needed.
/// </summary>
/// <remarks>
/// Custom fields are stored via <see cref="ReferenceDataEntity.MetadataJson"/>
/// or promoted to SQL columns via <c>MapProperty&lt;T&gt;()</c>.
/// </remarks>
public sealed class DynamicReferenceDataEntity : ReferenceDataEntity;
