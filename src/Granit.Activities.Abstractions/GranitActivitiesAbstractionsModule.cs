using Granit.Modularity;

namespace Granit.Activities;

/// <summary>
/// Granit module marker for the activities abstractions package. Same pattern
/// as <c>GranitWorkspacesAbstractionsModule</c> — pull this from any base module
/// that contributes activity types via <see cref="IActivityTypeProvider"/> or
/// that declares <c>.Activities(...)</c> on an <c>EntityDefinition</c>; pull
/// <c>GranitActivitiesModule</c> only from hosts that resolve and persist
/// activities (story A2).
/// </summary>
public sealed class GranitActivitiesAbstractionsModule : GranitModule;
