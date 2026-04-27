using System.Text.Json.Nodes;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.Http.ApiDocumentation;

namespace Granit.BlobStorage.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for BlobStorage Request and Response DTOs.
/// </summary>
internal sealed class BlobStorageSchemaExampleProvider : ISchemaExampleProvider
{
    private const string ContainerNameProperty = "containerName";
    private const string ExampleContainerName = "medical-images";
    private const string PngContentType = "image/png";

    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(BlobUploadInitiateRequest)] = new JsonObject
            {
                [ContainerNameProperty] = ExampleContainerName,
                ["fileName"] = "xray-2026-04-20.png",
                ["contentType"] = PngContentType,
                ["sizeBytes"] = 2_458_112L,
            },
            [typeof(BlobUploadInitiateResponse)] = new JsonObject
            {
                ["blobId"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                ["uploadUrl"] = "https://storage.acme.example/medical-images/01960f3a...?sig=...",
                ["httpMethod"] = "PUT",
                ["expiresAt"] = "2026-04-20T15:30:00+00:00",
                ["requiredHeaders"] = new JsonObject
                {
                    ["x-ms-blob-type"] = "BlockBlob",
                    ["content-type"] = PngContentType,
                },
            },
            [typeof(BlobConfirmUploadRequest)] = new JsonObject
            {
                [ContainerNameProperty] = ExampleContainerName,
            },
            [typeof(BlobDeleteRequest)] = new JsonObject
            {
                [ContainerNameProperty] = ExampleContainerName,
                ["deletionReason"] = "GDPR Art. 17 erasure request",
            },
            [typeof(BlobDownloadUrlRequest)] = new JsonObject
            {
                [ContainerNameProperty] = ExampleContainerName,
                ["fileName"] = "xray-2026-04-20.png",
            },
            [typeof(BlobDescriptorResponse)] = new JsonObject
            {
                ["id"] = "01960f3a-5c9e-7c3b-b4a2-abc123def456",
                [ContainerNameProperty] = ExampleContainerName,
                ["originalFileName"] = "xray-2026-04-20.png",
                ["declaredContentType"] = PngContentType,
                ["verifiedContentType"] = PngContentType,
                ["declaredSizeBytes"] = 2_458_112L,
                ["actualSizeBytes"] = 2_458_112L,
                ["status"] = "Validated",
                ["rejectionReason"] = null,
                ["deletionReason"] = null,
                ["createdAt"] = "2026-04-20T14:22:30+00:00",
                ["validatedAt"] = "2026-04-20T14:22:45+00:00",
                ["deletedAt"] = null,
            },
        };
}
