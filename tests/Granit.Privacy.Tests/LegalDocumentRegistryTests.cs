using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class LegalDocumentRegistryTests
{
    private readonly LegalDocumentRegistry _sut = new();

    [Fact]
    public void Register_AddsDocument()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));

        _sut.GetDefinition("privacy-policy").ShouldNotBeNull();
    }

    [Fact]
    public void Register_DuplicateId_ThrowsInvalidOperationException()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));

        Action act = () => _sut.Register(new LegalDocumentDefinition("privacy-policy", "2.0.0", "Privacy Policy v2"));

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("already registered");
    }

    [Fact]
    public void GetDefinition_UnknownDocument_ReturnsNull() =>
        _sut.GetDefinition("unknown").ShouldBeNull();

    [Fact]
    public void GetDefinition_IsCaseInsensitive()
    {
        _sut.Register(new LegalDocumentDefinition("Privacy-Policy", "1.0.0", "Privacy Policy"));

        _sut.GetDefinition("privacy-policy").ShouldNotBeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));
        _sut.Register(new LegalDocumentDefinition("terms", "1.0.0", "Terms of Service"));

        _sut.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void Register_NullDefinition_ThrowsArgumentNullException()
    {
        Action act = () => _sut.Register(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void GetAll_Empty_ReturnsEmptyList()
    {
        IReadOnlyList<LegalDocumentDefinition> result = _sut.GetAll();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetDefinition_ReturnsCorrectDefinition()
    {
        LegalDocumentDefinition definition = new("privacy-policy", "2.1.0", "Privacy Policy v2.1");
        _sut.Register(definition);

        LegalDocumentDefinition? result = _sut.GetDefinition("privacy-policy");

        result.ShouldNotBeNull();
        result!.CurrentVersion.ShouldBe("2.1.0");
        result.DisplayName.ShouldBe("Privacy Policy v2.1");
    }
}
