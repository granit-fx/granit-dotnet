namespace Granit.AI.Tools;

/// <summary>
/// A signal from a tool that the agentic loop must stop and hand a structured payload back to the
/// caller instead of feeding the tool result to the model and continuing (ADR-067). The
/// orchestrator is agnostic to the <see cref="Kind"/> — it surfaces the interrupt on
/// <see cref="AIOrchestrationResult.Interrupt"/> and the caller decides what to do. The first user
/// is the chat clarification flow (<c>request_clarification</c>), which blocks the turn until the
/// user answers.
/// </summary>
/// <param name="Kind">A discriminator for the interrupt, e.g. <c>clarification</c>.</param>
/// <param name="Payload">The interrupt's structured payload (typically JSON) for the caller to parse.</param>
public sealed record AIToolInterrupt(string Kind, string Payload);
