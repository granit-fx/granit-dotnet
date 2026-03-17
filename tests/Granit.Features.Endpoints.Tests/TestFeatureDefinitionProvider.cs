using Granit.Features.Definitions;

namespace Granit.Features.Endpoints.Tests;

/// <summary>
/// Test provider that declares the same features used in the test fixture,
/// so the definitions endpoint can reconstruct group structure.
/// </summary>
internal sealed class TestFeatureDefinitionProvider : IFeatureDefinitionProvider
{
    public void Define(IFeatureDefinitionContext context)
    {
        FeatureGroupDefinition acme = context.AddGroup("Acme", "Acme Features");

        acme.AddToggle("Acme.VideoConference", defaultValue: false, displayName: "Video Conference");
        acme.AddNumeric("Acme.MaxUsers", defaultValue: 50, min: 1, max: 10_000, displayName: "Max Users");
        acme.AddSelection("Acme.Theme", "light", ["light", "dark", "auto"], displayName: "Theme");
    }
}
