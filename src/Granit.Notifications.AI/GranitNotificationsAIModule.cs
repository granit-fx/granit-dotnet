using Granit.AI;
using Granit.Modularity;

namespace Granit.Notifications.AI;

/// <summary>
/// Granit module for AI-powered notification content generation and smart channel routing.
/// </summary>
/// <remarks>
/// <para>
/// Adds <see cref="IAINotificationContentGenerator"/> for LLM-based notification subject and body
/// generation, and <see cref="IAIChannelSelector"/> for intelligent delivery channel recommendation
/// based on notification severity, time of day, and available channels.
/// </para>
/// <para>
/// Requires <c>Granit.AI</c> core services and at least one AI provider to be registered.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsAIModule : GranitModule;
