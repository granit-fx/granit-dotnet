using Granit.BlobStorage.S3.Internal;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

/// <summary>
/// Guards the symmetry between the headers signed into the S3 presigned URL and the
/// <c>RequiredHeaders</c> echoed back to the client — a divergence would resurface the
/// <c>400 SignatureDoesNotMatch</c> bug from the showcase.
/// </summary>
public sealed class S3UploadMetadataBuilderTests
{
    [Fact]
    public void Build_AlwaysIncludesOriginalFileNameAndDeclaredContentType()
    {
        BlobUploadRequest request = new(
            FileName: "invoice.pdf",
            ContentType: "application/pdf",
            MaxAllowedBytes: 1024);

        Dictionary<string, string> result = S3UploadMetadataBuilder.Build(request);

        result["x-amz-meta-original-filename"].ShouldBe("invoice.pdf");
        result["x-amz-meta-declared-content-type"].ShouldBe("application/pdf");
    }

    [Fact]
    public void Build_PrefixesCustomMetadataWithXAmzMeta()
    {
        Dictionary<string, string> custom = new(StringComparer.Ordinal)
        {
            ["tenant-id"] = "acme",
            ["case-id"] = "42",
        };
        BlobUploadRequest request = new(
            FileName: "f.txt",
            ContentType: "text/plain",
            MaxAllowedBytes: 1,
            Metadata: custom);

        Dictionary<string, string> result = S3UploadMetadataBuilder.Build(request);

        result["x-amz-meta-tenant-id"].ShouldBe("acme");
        result["x-amz-meta-case-id"].ShouldBe("42");
    }

    [Fact]
    public void Build_WithNullMetadata_OnlyEmitsBuiltInHeaders()
    {
        BlobUploadRequest request = new("f.txt", "text/plain", 1);

        Dictionary<string, string> result = S3UploadMetadataBuilder.Build(request);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_KeyLookup_IsCaseInsensitive()
    {
        // The AWS SDK canonicalises signed headers to lowercase; downstream code must be able to
        // round-trip the dictionary regardless of casing.
        BlobUploadRequest request = new("f.txt", "text/plain", 1);

        Dictionary<string, string> result = S3UploadMetadataBuilder.Build(request);

        result.ContainsKey("X-Amz-Meta-Original-FileName").ShouldBeTrue();
    }

    [Fact]
    public void Build_CustomMetadataCollidesWithBuiltIn_CustomWins()
    {
        // Reasonable: callers can override the declared filename/content-type by passing the
        // same suffix. Behaviour pinned so a future refactor doesn't silently flip it.
        Dictionary<string, string> custom = new(StringComparer.Ordinal)
        {
            ["original-filename"] = "renamed.pdf",
        };
        BlobUploadRequest request = new("first.pdf", "application/pdf", 1, custom);

        Dictionary<string, string> result = S3UploadMetadataBuilder.Build(request);

        result["x-amz-meta-original-filename"].ShouldBe("renamed.pdf");
    }
}
