using Granit.Authentication.Oidc.Pkce;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class PkceHelperTests
{
    [Fact]
    public void GenerateCodeVerifier_ReturnsBase64UrlString()
    {
        string verifier = PkceHelper.GenerateCodeVerifier();

        verifier.ShouldNotBeNullOrEmpty();
        verifier.ShouldNotContain("+");
        verifier.ShouldNotContain("/");
        verifier.ShouldNotContain("=");
    }

    [Fact]
    public void GenerateCodeVerifier_IsUniqueEachCall()
    {
        string first = PkceHelper.GenerateCodeVerifier();
        string second = PkceHelper.GenerateCodeVerifier();

        first.ShouldNotBe(second);
    }

    [Fact]
    public void ComputeCodeChallenge_ProducesCorrectSha256()
    {
        // Known test vector: SHA-256 of "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        // is "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" (RFC 7636 Appendix B)
        const string codeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        string challenge = PkceHelper.ComputeCodeChallenge(codeVerifier);

        challenge.ShouldBe("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public void ComputeCodeChallenge_ResultContainsNoUnsafeCharacters()
    {
        string verifier = PkceHelper.GenerateCodeVerifier();

        string challenge = PkceHelper.ComputeCodeChallenge(verifier);

        challenge.ShouldNotContain("+");
        challenge.ShouldNotContain("/");
        challenge.ShouldNotContain("=");
    }
}
