namespace Granit.Webhooks.Internal;

/// <summary>
/// Produces a Stripe-style masked preview of a signing secret (e.g.
/// <c>whsec_b46a****************5182</c>) for admin UIs. Computed once from the
/// plaintext at creation / rotation time and persisted alongside the protected
/// secret — never derived from the hashed value (impossible by design).
/// </summary>
internal static class WebhookSecretHint
{
    /// <summary>Fixed visual length of the masked middle section.</summary>
    private const int MiddleStars = 16;

    /// <summary>Number of plaintext characters kept on each edge.</summary>
    private const int EdgeChars = 4;

    /// <summary>Recognised secret prefix produced by the signing-secret generator.</summary>
    private const string KnownPrefix = "whsec_";

    /// <summary>
    /// Returns a masked preview of <paramref name="plaintext"/>. The known
    /// <c>whsec_</c> prefix is preserved; the next 4 hex characters and the last 4
    /// are kept, with a 16-character mask in between. For unusually short inputs
    /// (≤ 8 body characters after the prefix), the entire body is replaced by
    /// asterisks so no two adjacent plaintext chunks leak.
    /// </summary>
    public static string From(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        bool hasPrefix = plaintext.StartsWith(KnownPrefix, StringComparison.Ordinal);
        string head = hasPrefix ? KnownPrefix : string.Empty;
        ReadOnlySpan<char> body = plaintext.AsSpan(head.Length);

        if (body.Length <= 2 * EdgeChars)
        {
            return string.Concat(head, new string('*', body.Length));
        }

        return string.Concat(
            head,
            body[..EdgeChars],
            new string('*', MiddleStars),
            body[^EdgeChars..]);
    }
}
