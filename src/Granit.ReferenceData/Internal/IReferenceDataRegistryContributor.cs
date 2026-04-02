namespace Granit.ReferenceData.Internal;

/// <summary>
/// Marker interface for deferred registry population at startup.
/// Each contributor registers one or more <see cref="ReferenceDataTypeRegistration"/>
/// instances into the <see cref="ReferenceDataRegistry"/>.
/// </summary>
internal interface IReferenceDataRegistryContributor
{
    void Configure(ReferenceDataRegistry registry);
}
