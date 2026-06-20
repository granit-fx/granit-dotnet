namespace Granit.Mentions.Internal;

/// <summary>
/// A registration tag marking an existing <c>ILookupSource</c> (by <paramref name="Name"/>) as
/// mentionable. The facade fans the <c>@</c> picker out only over the tagged sources — a mention is
/// just a lookup source opted into the picker, never a separate contract.
/// </summary>
/// <param name="Name">The lookup source name (e.g. <c>user</c>, <c>invoice</c>).</param>
internal sealed record MentionSource(string Name);
