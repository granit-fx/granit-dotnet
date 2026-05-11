namespace Granit.Documents.AssetMetadata.Domain;

/// <summary>Lifecycle of a <see cref="DocumentAssetMetadata"/> row.</summary>
public enum AssetMetadataStatus
{
    /// <summary>Row created, extraction not yet started.</summary>
    Pending = 0,

    /// <summary>Extractors are running.</summary>
    Extracting = 1,

    /// <summary>Extraction completed, typed + raw columns populated.</summary>
    Ready = 2,

    /// <summary>Extraction failed terminally; see <see cref="DocumentAssetMetadata.FailureReason"/>.</summary>
    Failed = 3,
}
