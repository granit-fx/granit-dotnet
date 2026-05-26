using Granit.AI.Extraction.Redaction;
using Shouldly;

namespace Granit.AI.Extraction.Tests;

public sealed class NoOpAIContentRedactorTests
{
    [Fact]
    public void Redact_returns_content_unchanged()
    {
        // The framework default is identity by design — no regex pattern set is
        // generic enough to be safe across all corpora, see IAIContentRedactor remarks.
        // Pinning this guarantees hosts that didn't override the seam keep their
        // current behaviour after a future framework upgrade.
        NoOpAIContentRedactor redactor = new();
        const string input = "Contact me at jdoe@example.com or +33 6 12 34 56 78.";

        redactor.Redact(input).ShouldBe(input);
    }
}
