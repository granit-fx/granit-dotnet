namespace Granit.Privacy.Regulations.Profiles;

/// <summary>
/// Extension point for declaring regulation profiles at startup.
/// The framework ships built-in providers for Tier 1 and Tier 2 regulations.
/// Applications add Tier 3 regulations via custom implementations.
/// </summary>
/// <remarks>
/// Implementations are auto-discovered from loaded module assemblies
/// by <see cref="IRegulationProfileRegistry"/>.
/// </remarks>
public interface IRegulationProfileProvider
{
    /// <summary>Declares regulation profiles via the context.</summary>
    /// <param name="context">The registration context.</param>
    void Define(IRegulationProfileContext context);
}
