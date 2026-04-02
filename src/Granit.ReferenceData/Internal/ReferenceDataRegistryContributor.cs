namespace Granit.ReferenceData.Internal;

/// <summary>
/// Default contributor that registers a single <see cref="ReferenceDataTypeRegistration"/>
/// into the <see cref="ReferenceDataRegistry"/> at startup.
/// </summary>
internal sealed class ReferenceDataRegistryContributor(
    ReferenceDataTypeRegistration registration) : IReferenceDataRegistryContributor
{
    public void Configure(ReferenceDataRegistry registry) =>
        registry.Register(registration);
}
