using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.Privacy.LegalAgreements.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class LegalAgreementCheckerTests
{
    private readonly LegalDocumentRegistry _documentRegistry = new();
    private readonly ILegalAgreementStoreReader _store = Substitute.For<ILegalAgreementStoreReader>();
    private readonly LegalAgreementChecker _sut;

    public LegalAgreementCheckerTests()
    {
        _documentRegistry.Register(new LegalDocumentDefinition("privacy-policy", "2.0.0", "Privacy Policy"));
        _sut = new LegalAgreementChecker(_documentRegistry, _store);
    }

    [Fact]
    public async Task HasAcceptedLatestAsync_UserAcceptedCurrentVersion_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        _store.FindLatestAsync(userId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(CreateAgreement(userId, "privacy-policy", "2.0.0"));

        bool result = await _sut.HasAcceptedLatestAsync(userId, "privacy-policy", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasAcceptedLatestAsync_UserAcceptedOldVersion_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _store.FindLatestAsync(userId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns(CreateAgreement(userId, "privacy-policy", "1.0.0"));

        bool result = await _sut.HasAcceptedLatestAsync(userId, "privacy-policy", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasAcceptedLatestAsync_UserNeverConsented_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _store.FindLatestAsync(userId, "privacy-policy", Arg.Any<CancellationToken>())
            .Returns((LegalAgreementBase?)null);

        bool result = await _sut.HasAcceptedLatestAsync(userId, "privacy-policy", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasAcceptedLatestAsync_UnknownDocument_ReturnsFalse()
    {
        var userId = Guid.NewGuid();

        bool result = await _sut.HasAcceptedLatestAsync(userId, "unknown-doc", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetUserAgreementsAsync_DelegatestoStore()
    {
        var userId = Guid.NewGuid();
        List<LegalAgreementBase> expected =
        [
            CreateAgreement(userId, "privacy-policy", "2.0.0"),
            CreateAgreement(userId, "privacy-policy", "1.0.0"),
            CreateAgreement(userId, "terms", "1.0.0")
        ];
        _store.FindAllByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(expected);

        IReadOnlyList<LegalAgreementBase> result = await _sut.GetUserAgreementsAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
    }

    private static TestLegalAgreement CreateAgreement(Guid userId, string documentId, string version) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = documentId,
            Version = version,
            AcceptedAt = DateTimeOffset.UtcNow
        };

    private sealed class TestLegalAgreement : LegalAgreementBase;
}
