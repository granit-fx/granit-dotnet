using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Subscriptions.Wolverine;

/// <summary>
/// Wolverine integration for Granit.Subscriptions. Handles provider sync after
/// FSM transitions and processes inbound external events (webhooks).
/// </summary>
[DependsOn(
    typeof(GranitSubscriptionsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitSubscriptionsWolverineModule : GranitModule;
