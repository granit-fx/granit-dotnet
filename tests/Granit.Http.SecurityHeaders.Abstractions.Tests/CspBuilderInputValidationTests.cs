using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

/// <summary>
/// Validates that <see cref="CspBuilder"/> rejects sources that could enable
/// CSP injection — directive separators and control characters.
/// </summary>
public sealed class CspBuilderInputValidationTests
{
    public static TheoryData<string> ForbiddenCharSamples =>
    [
        "'self'; default-src 'unsafe-inline'",  // directive separator
        "'self'\nscript-src 'unsafe-inline'",   // LF
        "'self'\r\nscript-src",                 // CRLF
        "'self'\0attack",                       // NUL
        "'self'\x01",                           // C0 control
        "'self'\x7F",                           // DEL
    ];

    [Theory]
    [MemberData(nameof(ForbiddenCharSamples))]
    public void AddScriptSrc_RejectsForbiddenCharacters(string malicious)
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.AddScriptSrc(malicious));
    }

    [Theory]
    [MemberData(nameof(ForbiddenCharSamples))]
    public void AddImgSrc_RejectsForbiddenCharacters(string malicious)
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.AddImgSrc(malicious));
    }

    [Theory]
    [MemberData(nameof(ForbiddenCharSamples))]
    public void AddDefaultSrc_RejectsForbiddenCharacters(string malicious)
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.AddDefaultSrc(malicious));
    }

    [Theory]
    [MemberData(nameof(ForbiddenCharSamples))]
    public void SetReportUri_RejectsForbiddenCharacters(string malicious)
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.SetReportUri(malicious));
    }

    [Theory]
    [MemberData(nameof(ForbiddenCharSamples))]
    public void SetReportTo_RejectsForbiddenCharacters(string malicious)
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.SetReportTo(malicious));
    }

    [Fact]
    public void AddScriptSrc_ParamsArray_RejectsFirstMaliciousSource()
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() =>
            b.AddScriptSrc("'self'", "'unsafe-inline'; default-src", "data:"));
    }

    [Fact]
    public void AddScriptSrc_AcceptsValidSources()
    {
        CspBuilder b = new();
        Should.NotThrow(() => b.AddScriptSrc(
            "'self'",
            "'unsafe-inline'",
            "'nonce-Y4qfLcLg=='",
            "'sha256-AbCdEf=='",
            "https://example.com",
            "https://*.scalar.com",
            "data:",
            "blob:"));
    }

    [Fact]
    public void AddScriptSrc_NullOrEmptySource_Throws()
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.AddScriptSrc(string.Empty));
    }
}
