using Granit.Guids;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Endpoints;

/// <summary>
/// Online duplicate-detection guard on <see cref="PartyEndpoints.HandleCreateAsync"/>
/// (story #1302). Verifies the four behaviours that matter to clients:
/// <list type="bullet">
///   <item>Tier-1 hit → 409 with structured candidates body</item>
///   <item>Only fuzzy hits → no block (defer to recurring scan)</item>
///   <item><c>?force=true</c> → bypass</item>
///   <item><c>X-Skip-Duplicate-Check</c> header → bypass (bulk migrations)</item>
/// </list>
/// </summary>
public sealed class PartyEndpointsCreateOnlineDedupTests
{
    private readonly IPartyWriter _writer = Substitute.For<IPartyWriter>();
    private readonly IPartyDuplicateDetector _detector = Substitute.For<IPartyDuplicateDetector>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly PartyCreateRequest _request;

    public PartyEndpointsCreateOnlineDedupTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _request = new PartyCreateRequest(
            Kind: PartyKind.Company,
            Name: "Acme",
            DefaultCurrency: "EUR",
            TaxId: "BE0123456789");
    }

    [Fact]
    public async Task Returns_409_when_Tier1_match_detected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var existingId = Guid.NewGuid();
        _detector.FindCandidatesAsync(Arg.Any<PartyDraft>(), ct)
            .Returns(new List<DuplicateCandidate>
            {
                new(PartyId.Create(existingId), 1.0m, DuplicateMatchTier.Deterministic,
                    [new MatchSignal("TaxIdExact", 1.0m)])
            });

        Results<Created<PartyResponse>, Conflict<PartyCreateConflictResponse>, ValidationProblem>
            response = await PartyEndpoints.HandleCreateAsync(
                _request, force: false, skipDuplicateCheck: false,
                _writer, _guidGenerator, _detector, ct);

        Conflict<PartyCreateConflictResponse> conflict =
            response.Result.ShouldBeOfType<Conflict<PartyCreateConflictResponse>>();
        conflict.Value!.Reason.ShouldBe("DuplicatesDetected");
        conflict.Value!.Candidates.Count.ShouldBe(1);
        conflict.Value!.Candidates[0].CandidateId.ShouldBe(existingId);
        conflict.Value!.Candidates[0].Tier.ShouldBe("Deterministic");

        // Writer NEVER called — the create was blocked.
        await _writer.DidNotReceiveWithAnyArgs().AddAsync(default!, ct);
    }

    [Fact]
    public async Task Does_not_block_when_only_fuzzy_matches_present()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _detector.FindCandidatesAsync(Arg.Any<PartyDraft>(), ct)
            .Returns(new List<DuplicateCandidate>
            {
                new(PartyId.Create(Guid.NewGuid()), 0.85m, DuplicateMatchTier.Fuzzy,
                    [new MatchSignal("NameTokenSet", 0.4m)])
            });

        Results<Created<PartyResponse>, Conflict<PartyCreateConflictResponse>, ValidationProblem>
            response = await PartyEndpoints.HandleCreateAsync(
                _request, force: false, skipDuplicateCheck: false,
                _writer, _guidGenerator, _detector, ct);

        // Tier-3 fuzzy doesn't block — create proceeds. Recurring scan surfaces it later.
        response.Result.ShouldBeOfType<Created<PartyResponse>>();
        await _writer.Received(1).AddAsync(Arg.Any<Party>(), ct);
    }

    [Fact]
    public async Task Bypasses_check_when_force_true()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        // Detector would normally block, but force=true skips the check entirely.
        _detector.FindCandidatesAsync(Arg.Any<PartyDraft>(), ct)
            .Returns(new List<DuplicateCandidate>
            {
                new(PartyId.Create(Guid.NewGuid()), 1.0m, DuplicateMatchTier.Deterministic, [])
            });

        Results<Created<PartyResponse>, Conflict<PartyCreateConflictResponse>, ValidationProblem>
            response = await PartyEndpoints.HandleCreateAsync(
                _request, force: true, skipDuplicateCheck: false,
                _writer, _guidGenerator, _detector, ct);

        response.Result.ShouldBeOfType<Created<PartyResponse>>();
        // Detector never called — short-circuited before the round-trip.
        await _detector.DidNotReceiveWithAnyArgs().FindCandidatesAsync(default!, ct);
    }

    [Fact]
    public async Task Bypasses_check_when_X_Skip_Duplicate_Check_header_true()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _detector.FindCandidatesAsync(Arg.Any<PartyDraft>(), ct)
            .Returns(new List<DuplicateCandidate>
            {
                new(PartyId.Create(Guid.NewGuid()), 1.0m, DuplicateMatchTier.Deterministic, [])
            });

        Results<Created<PartyResponse>, Conflict<PartyCreateConflictResponse>, ValidationProblem>
            response = await PartyEndpoints.HandleCreateAsync(
                _request, force: false, skipDuplicateCheck: true,
                _writer, _guidGenerator, _detector, ct);

        response.Result.ShouldBeOfType<Created<PartyResponse>>();
        await _detector.DidNotReceiveWithAnyArgs().FindCandidatesAsync(default!, ct);
    }
}
