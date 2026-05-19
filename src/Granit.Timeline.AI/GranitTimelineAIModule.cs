using Granit.AI;
using Granit.Modularity;

namespace Granit.Timeline.AI;

/// <summary>
/// Granit module for AI-powered timeline summarization and anomaly detection.
/// </summary>
/// <remarks>
/// <para>
/// Provides <see cref="ITimelineSummarizer"/> for generating natural language summaries
/// of activity streams, and <see cref="ITimelineAnomalyDetector"/> for detecting unusual
/// patterns such as bulk edits, off-hours activity, and privilege escalation.
/// </para>
/// <para>
/// Requires <c>Granit.AI</c> core services and at least one AI provider
/// to be registered beforehand.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitTimelineModule))]
public sealed class GranitTimelineAIModule : GranitModule;
