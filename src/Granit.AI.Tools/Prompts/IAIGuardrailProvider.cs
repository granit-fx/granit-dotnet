namespace Granit.AI.Tools.Prompts;

/// <summary>
/// Supplies the framework guardrail prompt — the security-critical preamble that keeps the agent
/// inside its ACL scope and treats tool/document output as data, never instructions (ADR-067).
/// </summary>
/// <remarks>
/// Guardrails are <strong>code-first and versioned</strong>: there is deliberately no setter and
/// no tenant-facing API to edit them. A tenant must never be able to weaken the preamble. The
/// default implementation returns a compile-time constant.
/// </remarks>
public interface IAIGuardrailProvider
{
    /// <summary>The current framework guardrails, with their version.</summary>
    AIPromptVersion Guardrails { get; }
}
