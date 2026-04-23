using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Privacy.Endpoints.Dtos;

namespace Granit.Privacy.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Privacy Request DTOs (GDPR consent, erasure,
/// legal documents).
/// </summary>
internal sealed class PrivacySchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(PrivacyAcceptAgreementRequest)] = new JsonObject
            {
                ["documentId"] = "privacy-policy",
                ["version"] = "2.1.0",
            },
            [typeof(PrivacyDeletionRequest)] = new JsonObject
            {
                ["reason"] = "Account closure — withdrawal of consent.",
                ["defer"] = true,
            },
            [typeof(LegalDocumentCreateRequest)] = new JsonObject
            {
                ["documentId"] = "privacy-policy",
                ["displayName"] = "Privacy Policy",
                ["description"] = "Initial draft based on GDPR Art. 13 disclosure requirements.",
                ["templateName"] = "PrivacyPolicy",
            },
            [typeof(LegalDocumentUpdateRequest)] = new JsonObject
            {
                ["displayName"] = "Privacy Policy",
                ["description"] = "Added section on third-party analytics.",
                ["templateName"] = "PrivacyPolicy",
                ["documentBlobId"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
            },
        };
}
