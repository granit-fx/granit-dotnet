using System.Text.Json.Nodes;
using Granit.AI.Endpoints.Dtos;
using Granit.Http.ApiDocumentation;

namespace Granit.AI.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for AI Request and Response DTOs (chat, embeddings, workspaces).
/// </summary>
internal sealed class AISchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            // ──── Chat ────
            [typeof(AIChatRequest)] = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = "You are a concise assistant that answers in one sentence.",
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = "Summarize RFC 7807 in one sentence.",
                    },
                },
            },
            [typeof(AIChatMessageRequest)] = new JsonObject
            {
                ["role"] = "user",
                ["content"] = "What is the capital of Belgium?",
            },

            // ──── Embeddings ────
            [typeof(AIEmbeddingRequest)] = new JsonObject
            {
                ["inputs"] = new JsonArray
                {
                    "The quick brown fox jumps over the lazy dog.",
                    "Granit is a modular .NET 10 framework.",
                },
            },

            // ──── Workspaces ────
            [typeof(AIWorkspaceCreateRequest)] = new JsonObject
            {
                ["name"] = "support-triage",
                ["provider"] = "OpenAI",
                ["model"] = "gpt-4o",
                ["systemPrompt"] = "You classify incoming support tickets into one of: billing, bug, feature-request, other.",
                ["temperature"] = 0.2f,
                ["maxOutputTokens"] = 512,
            },
            [typeof(AIWorkspaceUpdateRequest)] = new JsonObject
            {
                ["provider"] = "OpenAI",
                ["model"] = "gpt-4o-mini",
                ["systemPrompt"] = "You classify incoming support tickets into one of: billing, bug, feature-request, other.",
                ["temperature"] = 0.2f,
                ["maxOutputTokens"] = 512,
                ["activated"] = true,
            },
        };
}
