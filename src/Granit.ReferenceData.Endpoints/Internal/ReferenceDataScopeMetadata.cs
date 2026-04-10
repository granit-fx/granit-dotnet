using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Endpoints.Internal;

/// <summary>
/// Endpoint metadata that stores the reference data scope for tenant context enforcement.
/// </summary>
/// <param name="Scope">The multi-tenancy scope declared for this reference data type.</param>
internal sealed record ReferenceDataScopeMetadata(ReferenceDataScope Scope);
