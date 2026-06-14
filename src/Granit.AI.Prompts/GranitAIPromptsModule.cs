using Granit.Modularity;

namespace Granit.AI.Prompts;

/// <summary>
/// Granit module for the user-facing prompt catalogue (ADR-067): the
/// <see cref="Domain.PromptTemplate"/> aggregate and the <see cref="IPromptTemplateStore"/>
/// abstraction. Persistence is provided by <c>Granit.AI.Prompts.EntityFrameworkCore</c> and the
/// HTTP surface by <c>Granit.AI.Prompts.Endpoints</c>.
/// </summary>
public sealed class GranitAIPromptsModule : GranitModule;
