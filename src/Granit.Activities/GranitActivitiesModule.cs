using Granit.Modularity;

namespace Granit.Activities;

/// <summary>
/// Granit module marker for the activities runtime package. Pull this from
/// hosts that resolve, persist, or query activities; module-side declarations
/// (IActivityTypeProvider, EntityDefinition.Activities()) only need
/// <c>Granit.Activities.Abstractions</c>.
/// </summary>
[DependsOn(typeof(GranitActivitiesAbstractionsModule))]
public sealed class GranitActivitiesModule : GranitModule;
