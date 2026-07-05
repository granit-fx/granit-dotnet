namespace Granit.Features.Definitions;

/// <summary>
/// Contract for declaring application features in code.
/// </summary>
/// <remarks>
/// Implement this interface and register the implementation with:
/// <c>services.AddFeatureDefinitions&lt;MyFeatureDefinitionProvider&gt;();</c>
/// </remarks>
/// <example>
/// <code>
/// public sealed class AcmeFeatureDefinitionProvider : IFeatureDefinitionProvider
/// {
///     public void Define(IFeatureDefinitionContext context)
///     {
///         FeatureGroupDefinition acme = context.AddGroup("Acme", "Acme Features");
///
///         acme.AddToggle(AcmeFeatures.VideoConference.Name, defaultValue: false);
///         acme.AddNumeric(AcmeFeatures.MaxUsersCount.Name, defaultValue: 50, min: 1, max: 10_000);
///     }
/// }
/// </code>
/// </example>
public interface IFeatureDefinitionProvider
{
    /// <summary>
    /// Declares feature groups and features using <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The definition context for registering groups and features.</param>
    void Define(IFeatureDefinitionContext context);
}
