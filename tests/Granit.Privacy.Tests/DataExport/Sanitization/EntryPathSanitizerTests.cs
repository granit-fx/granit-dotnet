using Granit.Privacy.DataExport.Sanitization;
using Granit.Privacy.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Sanitization;

public class EntryPathSanitizerTests
{
    // ────────────────────────────────────────────────────────────────────────
    // Happy path — accepted inputs are returned in normalized form
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("identity-local.json", "identity-local.json")]
    [InlineData("Documents/2024/foo.pdf", "Documents/2024/foo.pdf")]
    [InlineData("a/b/c/d.txt", "a/b/c/d.txt")]
    [InlineData("Documents\\2024\\foo.pdf", "Documents/2024/foo.pdf")] // backslashes normalized
    [InlineData("Documents//2024///foo.pdf", "Documents/2024/foo.pdf")] // consecutive slashes collapsed
    [InlineData("a/b/", "a/b")] // trailing slash stripped
    public void Sanitize_returns_normalized_path_for_valid_input(string input, string expected) =>
        EntryPathSanitizer.Sanitize(input).ShouldBe(expected);

    [Theory]
    [InlineData("a.", "a")]          // trailing dot stripped per-segment (Windows compat)
    [InlineData("a ", "a")]          // trailing space stripped per-segment
    [InlineData("a/b./c.txt", "a/b/c.txt")] // strip applied per-segment, not globally
    public void Sanitize_strips_trailing_dots_and_spaces_per_segment(string input, string expected) =>
        EntryPathSanitizer.Sanitize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_throws_on_null_or_empty(string? input) =>
        Should.Throw<ArgumentException>(() => EntryPathSanitizer.Sanitize(input!));

    // ────────────────────────────────────────────────────────────────────────
    // Zip-slip — VULN-004 rejections
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("..")]
    [InlineData("../etc/passwd")]
    [InlineData("a/../b")]
    [InlineData("a/b/..")]
    [InlineData("..\\windows\\system32")]
    public void Sanitize_rejects_parent_traversal(string input)
    {
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));
        ex.Reason.ShouldContain("..");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("./foo")]
    [InlineData("a/./b")]
    public void Sanitize_rejects_current_directory_segment(string input) =>
        Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));

    [Theory]
    [InlineData("/absolute/path")]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\path")] // becomes "/windows/path" after backslash normalization
    public void Sanitize_rejects_absolute_paths(string input)
    {
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));
        ex.Reason.ShouldContain("absolute");
    }

    [Theory]
    [InlineData("C:")]
    [InlineData("C:/Users/foo")]
    [InlineData("D:\\data\\file.txt")]
    [InlineData("z:foo")]
    public void Sanitize_rejects_windows_drive_letters(string input)
    {
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));
        ex.Reason.ShouldContain("drive");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Control characters
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("foo\0bar")] // NUL
    [InlineData("foo\rbar")] // CR
    [InlineData("foo\nbar")] // LF
    [InlineData("foo\tbar")] // TAB (< 0x20)
    [InlineData("foobar")]
    [InlineData("foobar")]
    [InlineData("foobar")] // DEL
    public void Sanitize_rejects_control_characters(string input)
    {
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));
        ex.Reason.ShouldContain("control");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Windows reserved names (CON, PRN, AUX, NUL, COM[0-9], LPT[0-9])
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    [InlineData("con")]     // case-insensitive
    [InlineData("CON.txt")] // reserved even with an extension
    [InlineData("NUL.log")]
    [InlineData("docs/CON/file.txt")] // anywhere in the path
    public void Sanitize_rejects_windows_reserved_names(string input)
    {
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(input));
        ex.Reason.ShouldContain("reserved");
    }

    [Theory]
    [InlineData("CONFIG")] // not reserved — "CON" + "FIG"
    [InlineData("PRINTER")]
    [InlineData("AUXILIARY")]
    [InlineData("LPT99")] // only LPT0-LPT9 are reserved
    [InlineData("COM10")] // only COM0-COM9 are reserved
    public void Sanitize_accepts_names_that_only_start_with_reserved_stems(string input) =>
        EntryPathSanitizer.Sanitize(input).ShouldBe(input);

    // ────────────────────────────────────────────────────────────────────────
    // Length caps
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_rejects_segment_above_255_bytes()
    {
        string longSegment = new('a', EntryPathSanitizer.MaxSegmentBytes + 1);
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(longSegment));
        ex.Reason.ShouldContain("segment");
    }

    [Fact]
    public void Sanitize_accepts_segment_at_255_bytes()
    {
        string maxSegment = new('a', EntryPathSanitizer.MaxSegmentBytes);
        EntryPathSanitizer.Sanitize(maxSegment).ShouldBe(maxSegment);
    }

    [Fact]
    public void Sanitize_counts_segment_length_in_utf8_bytes_not_chars()
    {
        // '🎉' (U+1F389) is 4 bytes UTF-8. 64 of them = 256 bytes > MaxSegmentBytes.
        string emoji = string.Concat(Enumerable.Repeat("🎉", 64));
        Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(emoji));
    }

    [Fact]
    public void Sanitize_rejects_full_path_above_4096_bytes()
    {
        // 17 segments of 250 'a' separated by '/' = 17 * 250 + 16 = 4266 bytes
        string segment = new('a', 250);
        string longPath = string.Join('/', Enumerable.Repeat(segment, 17));
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(longPath));
        ex.Reason.ShouldContain("path exceeds");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Exception payload sanity
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void InvalidExportEntryPathException_carries_original_input()
    {
        const string raw = "../etc/passwd";
        InvalidExportEntryPathException ex = Should.Throw<InvalidExportEntryPathException>(() => EntryPathSanitizer.Sanitize(raw));
        ex.EntryPath.ShouldBe(raw);
        ex.Reason.ShouldNotBeNullOrEmpty();
        ex.Message.ShouldContain(raw);
    }
}
