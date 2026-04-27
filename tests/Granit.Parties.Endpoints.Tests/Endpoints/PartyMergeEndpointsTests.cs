using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Endpoints;

/// <summary>
/// Unit tests for <see cref="PartyMergeEndpoints"/>. Substitutes the orchestrator and
/// validates that handlers translate orchestrator outcomes into the right HTTP shape:
/// <list type="bullet">
/// <item><see cref="MergeException"/> → 422.</item>
/// <item><see cref="InvalidOperationException"/> (idempotency conflict, race) → 409.</item>
/// <item>Successful result → 200 with the wire-shaped DTO.</item>
/// </list>
/// </summary>
public sealed class PartyMergeEndpointsTests
{
    private readonly IMergeService<Party> _mergeService = Substitute.For<IMergeService<Party>>();

    // ── Preview ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_ReturnsOk_WithMappedConflictsAndCounts()
    {
        var survivor = Guid.NewGuid();
        var loser = Guid.NewGuid();
        _mergeService.MergePreviewAsync(survivor, loser, Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts:
                [
                    new FieldConflict("Name", "Acme S", "Acme L", WinnerSide.Survivor),
                    new FieldConflict("TaxStatus", null, "Exempt", WinnerSide.Loser),
                ],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Invoice.PartyId"] = 17,
                    ["Subscription.PartyId"] = 3,
                },
                DryRun: true));

        Results<Ok<PartyMergeResponse>, ProblemHttpResult> result = await PartyMergeEndpoints.HandlePreviewAsync(
            survivor, loser, _mergeService, TestContext.Current.CancellationToken);

        Ok<PartyMergeResponse> ok = result.Result.ShouldBeOfType<Ok<PartyMergeResponse>>();
        PartyMergeResponse body = ok.Value!;
        body.SurvivorId.ShouldBe(survivor);
        body.LoserId.ShouldBe(loser);
        body.DryRun.ShouldBeTrue();
        body.Conflicts.Count.ShouldBe(2);
        body.Conflicts[0].FieldPath.ShouldBe("Name");
        body.Conflicts[0].SurvivorValue.ShouldBe("Acme S");
        body.Conflicts[0].Default.ShouldBe("Survivor");
        body.Conflicts[1].SurvivorValue.ShouldBeNull();
        body.RewriteCounts["Invoice.PartyId"].ShouldBe(17);
    }

    [Fact]
    public async Task Preview_Returns422_OnMergeException()
    {
        _mergeService.MergePreviewAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MergeException("Tenant mismatch."));

        Results<Ok<PartyMergeResponse>, ProblemHttpResult> result = await PartyMergeEndpoints.HandlePreviewAsync(
            Guid.NewGuid(), Guid.NewGuid(), _mergeService, TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    // ── Live merge ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Merge_PassesIdempotencyKeyAndChoicesToOrchestrator()
    {
        var survivor = Guid.NewGuid();
        var loser = Guid.NewGuid();
        const string idempotencyKey = "req-42";

        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal),
                DryRun: false));

        var request = new PartyMergeRequest(
            LoserId: loser,
            Choices: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Name"] = "Loser",
                ["Metadata.segment"] = "Survivor",
            },
            Reason: "Sync duplicate",
            DryRun: false);

        Results<Ok<PartyMergeResponse>, ProblemHttpResult, ValidationProblem> result = await PartyMergeEndpoints.HandleMergeAsync(
            survivor, request, idempotencyKey, _mergeService, TestContext.Current.CancellationToken);

        result.Result.ShouldBeOfType<Ok<PartyMergeResponse>>();

        await _mergeService.Received(1).MergeAsync(
            Arg.Is<MergeRequest>(r =>
                r.SurvivorId == survivor &&
                r.LoserId == loser &&
                r.IdempotencyKey == "req-42" &&
                r.Reason == "Sync duplicate" &&
                !r.DryRun &&
                r.Choices.Choices["Name"] == WinnerSide.Loser &&
                r.Choices.Choices["Metadata.segment"] == WinnerSide.Survivor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_Returns422_OnMergeException()
    {
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MergeException("Currency mismatch."));

        Results<Ok<PartyMergeResponse>, ProblemHttpResult, ValidationProblem> result = await PartyMergeEndpoints.HandleMergeAsync(
            Guid.NewGuid(),
            new PartyMergeRequest(LoserId: Guid.NewGuid()),
            idempotencyKey: null,
            _mergeService,
            TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Merge_Returns409_OnInvalidOperationException()
    {
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Loser already merged."));

        Results<Ok<PartyMergeResponse>, ProblemHttpResult, ValidationProblem> result = await PartyMergeEndpoints.HandleMergeAsync(
            Guid.NewGuid(),
            new PartyMergeRequest(LoserId: Guid.NewGuid()),
            idempotencyKey: null,
            _mergeService,
            TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Merge_DefaultsToEmptyChoices_WhenNoneSupplied()
    {
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal),
                DryRun: false));

        await PartyMergeEndpoints.HandleMergeAsync(
            Guid.NewGuid(),
            new PartyMergeRequest(LoserId: Guid.NewGuid(), Choices: null),
            idempotencyKey: null,
            _mergeService,
            TestContext.Current.CancellationToken);

        await _mergeService.Received(1).MergeAsync(
            Arg.Is<MergeRequest>(r => r.Choices.Choices.Count == 0),
            Arg.Any<CancellationToken>());
    }
}
