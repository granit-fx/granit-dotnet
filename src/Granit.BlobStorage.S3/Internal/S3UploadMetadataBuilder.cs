namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// Builds the canonical set of <c>x-amz-meta-*</c> headers attached to an S3 presigned PUT.
/// The same dictionary feeds both <see cref="Amazon.S3.Model.GetPreSignedUrlRequest.Metadata"/>
/// (driving the signature) and <see cref="PresignedUploadTicket.RequiredHeaders"/> (what the
/// client must echo on the wire), guaranteeing symmetry by construction.
/// </summary>
/// <remarks>
/// If the signed metadata diverges from <c>RequiredHeaders</c>, S3 / MinIO will recompute the
/// canonical request with a different header set and reject the PUT with
/// <c>400 SignatureDoesNotMatch</c>.
/// <para>
/// Values are stored verbatim — the AWS SDK does not URL-encode metadata values when signing.
/// Non-ASCII characters in <see cref="BlobUploadRequest.FileName"/> or
/// <see cref="BlobUploadRequest.Metadata"/> will be signed as raw bytes; callers responsible for
/// the upload must transmit those same bytes in the HTTP header, which is fragile and best
/// avoided.
/// </para>
/// </remarks>
internal static class S3UploadMetadataBuilder
{
    internal const string OriginalFileNameHeader = "x-amz-meta-original-filename";
    internal const string DeclaredContentTypeHeader = "x-amz-meta-declared-content-type";

    /// <summary>
    /// Returns the <c>x-amz-meta-*</c> headers to sign for the given upload request, keyed
    /// case-insensitively.
    /// </summary>
    internal static Dictionary<string, string> Build(BlobUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, string> signedMetadata = new(StringComparer.OrdinalIgnoreCase)
        {
            [OriginalFileNameHeader] = request.FileName,
            [DeclaredContentTypeHeader] = request.ContentType,
        };

        if (request.Metadata is not null)
        {
            foreach (KeyValuePair<string, string> entry in request.Metadata)
            {
                signedMetadata[$"x-amz-meta-{entry.Key}"] = entry.Value;
            }
        }

        return signedMetadata;
    }
}
