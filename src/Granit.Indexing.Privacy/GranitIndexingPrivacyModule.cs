using Granit.Modularity;
using Granit.Privacy;

namespace Granit.Indexing.Privacy;

/// <summary>
/// Granit module bridging <c>Granit.Indexing</c> erasers to the
/// <c>Granit.Privacy</c> deletion flow. Adding this module enables the cascade — without
/// it, indexed copies survive a GDPR deletion request.
/// </summary>
/// <remarks>
/// The Wolverine handler is auto-discovered through assembly export. No DI registration
/// is required at the module level.
/// </remarks>
[DependsOn(typeof(GranitIndexingModule), typeof(GranitPrivacyModule))]
public sealed class GranitIndexingPrivacyModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // No-op: handler is discovered by Wolverine; erasers are registered by each
        // backend's Add… extension.
    }
}
