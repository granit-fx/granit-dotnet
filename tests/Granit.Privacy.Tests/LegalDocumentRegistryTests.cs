using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class LegalDocumentRegistryTests
{
    private readonly LegalDocumentRegistry _sut = new();

    [Fact]
    public async Task Register_AddsDocument()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));

        (await _sut.GetDefinitionAsync("privacy-policy", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public void Register_DuplicateId_ThrowsInvalidOperationException()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));

        Action act = () => _sut.Register(new LegalDocumentDefinition("privacy-policy", "2.0.0", "Privacy Policy v2"));

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("already registered");
    }

    [Fact]
    public async Task GetDefinitionAsync_UnknownDocument_ReturnsNull() =>
        (await _sut.GetDefinitionAsync("unknown", TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task GetDefinitionAsync_IsCaseInsensitive()
    {
        _sut.Register(new LegalDocumentDefinition("Privacy-Policy", "1.0.0", "Privacy Policy"));

        (await _sut.GetDefinitionAsync("privacy-policy", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRegistered()
    {
        _sut.Register(new LegalDocumentDefinition("privacy-policy", "1.0.0", "Privacy Policy"));
        _sut.Register(new LegalDocumentDefinition("terms", "1.0.0", "Terms of Service"));

        (await _sut.GetAllAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(2);
    }

    [Fact]
    public void Register_NullDefinition_ThrowsArgumentNullException()
    {
        Action act = () => _sut.Register(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        IReadOnlyList<LegalDocumentDefinition> result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDefinitionAsync_ReturnsCorrectDefinition()
    {
        LegalDocumentDefinition definition = new("privacy-policy", "2.1.0", "Privacy Policy v2.1");
        _sut.Register(definition);

        LegalDocumentDefinition? result = await _sut.GetDefinitionAsync("privacy-policy", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.CurrentVersion.ShouldBe("2.1.0");
        result.DisplayName.ShouldBe("Privacy Policy v2.1");
    }
}
