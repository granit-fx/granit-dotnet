namespace Granit.Timeline.Domain.ValueObjects;

/// <summary>
/// Denormalized author identity captured at the time of posting.
/// </summary>
/// <param name="Id">User identifier of the author.</param>
/// <param name="Name">Display name of the author at the time of posting.</param>
public sealed record AuthorInfo(string Id, string Name);
