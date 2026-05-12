namespace Granit.Documents.PublicLinks.Internal;

/// <summary>
/// Raised by <see cref="IDocumentPublicLinkStore"/> implementations when an
/// optimistic concurrency check fails on update — typically two concurrent
/// redemptions racing against the same <c>MaxUses</c> cap.
/// </summary>
/// <remarks>
/// Domain-level wrapper so <see cref="Granit.Documents.PublicLinks.Internal.DocumentPublicLinkService"/>
/// (which lives in the base module, layer-pure) can react without taking a hard
/// dependency on <c>Microsoft.EntityFrameworkCore</c>.
/// </remarks>
internal sealed class PublicLinkConcurrencyConflictException : Exception
{
    public PublicLinkConcurrencyConflictException()
        : base("DocumentPublicLink update lost the optimistic concurrency race.")
    {
    }

    public PublicLinkConcurrencyConflictException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
