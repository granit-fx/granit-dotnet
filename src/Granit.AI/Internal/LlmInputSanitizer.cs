using System.Security.Cryptography;
using System.Text;

namespace Granit.AI.Internal;

/// <summary>
/// Shared utilities for sanitizing inputs before sending them to an LLM.
/// Complements <see cref="PromptBuilder"/> (which handles structural sanitization)
/// with domain-specific transformations like PII pseudonymization.
/// </summary>
internal static class LlmInputSanitizer
{
    /// <summary>
    /// One-way SHA-256 hash of the user ID, truncated to 16 hex characters.
    /// The LLM can still detect patterns for the same pseudonymized user
    /// without receiving the actual user identifier (GDPR Art. 5 data minimization).
    /// </summary>
    public static string PseudonymizeUserId(string userId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))[..16];
}
