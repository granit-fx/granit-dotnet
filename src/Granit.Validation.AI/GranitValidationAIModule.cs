using Granit.AI;
using Granit.Modularity;

namespace Granit.Validation.AI;

/// <summary>
/// Granit module for AI-powered content moderation and semantic validation.
/// </summary>
/// <remarks>
/// Registers <see cref="IAIContentModerator"/> as a scoped service backed by an LLM.
/// Validators can inject the moderator to analyze user-provided text for toxic content,
/// prompt injection attempts, and spam/gibberish. The service uses a fail-open design:
/// when the LLM is unavailable, content is accepted with a warning log for manual review.
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationAIModule : GranitModule;
