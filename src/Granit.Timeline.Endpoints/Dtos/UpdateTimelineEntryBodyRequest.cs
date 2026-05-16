namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Request DTO for PATCH /timeline/&#123;entityType&#125;/&#123;entityId&#125;/entries/&#123;entryId&#125;.
/// Replaces the body of an entry the caller authored within the edit window.
/// </summary>
public sealed record UpdateTimelineEntryBodyRequest
{
    /// <summary>New Markdown body. The four edit gates (origin, type, authorship, window) are enforced server-side.</summary>
    public required string Body { get; init; }
}
