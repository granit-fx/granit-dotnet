using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Guids;
using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Granit.Parties.Endpoints.Internal;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly IAuditingWriter _auditingWriter = Substitute.For<IAuditingWriter>();
    private readonly ILogger<PartyMergeRequest> _logger = NullLogger<PartyMergeRequest>.Instance;
    private readonly PartyMergeAuditWriter _auditWriter;

    public PartyMergeEndpointsTests()
    {
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("admin-1");
        user.UserName.Returns("Admin");

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 4, 27, 14, 0, 0, TimeSpan.Zero));

        _auditWriter = new PartyMergeAuditWriter(_auditingWriter, user, tenant, guidGen, clock);
    }

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
            survivor, request, idempotencyKey, _mergeService, _auditWriter, _logger, TestContext.Current.CancellationToken);

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
            _auditWriter,
            _logger,
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
            _auditWriter,
            _logger,
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
            _auditWriter,
            _logger,
            TestContext.Current.CancellationToken);

        await _mergeService.Received(1).MergeAsync(
            Arg.Is<MergeRequest>(r => r.Choices.Choices.Count == 0),
            Arg.Any<CancellationToken>());
    }

    // ── Audit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Merge_WritesAuditEntry_AfterSuccessfulLiveMerge()
    {
        var survivor = Guid.NewGuid();
        var loser = Guid.NewGuid();
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Invoice.PartyId"] = 12,
                    ["Subscription.PartyId"] = 1,
                },
                DryRun: false));

        await PartyMergeEndpoints.HandleMergeAsync(
            survivor,
            new PartyMergeRequest(
                LoserId: loser,
                Choices: new Dictionary<string, string>(StringComparer.Ordinal) { ["Name"] = "Survivor" },
                Reason: "Doublon CRM"),
            idempotencyKey: "k1",
            _mergeService,
            _auditWriter,
            _logger,
            TestContext.Current.CancellationToken);

        await _auditingWriter.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.Category == AuditCategory.DataMutation &&
                e.UserId == "admin-1" &&
                e.EntityChanges.Count == 1 &&
                e.EntityChanges.First().EntityType == "Party" &&
                e.EntityChanges.First().EntityId == survivor.ToString() &&
                e.EntityChanges.First().PropertyChanges.Count == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_DoesNotWriteAuditEntry_WhenDryRun()
    {
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal),
                DryRun: true));

        await PartyMergeEndpoints.HandleMergeAsync(
            Guid.NewGuid(),
            new PartyMergeRequest(LoserId: Guid.NewGuid(), DryRun: true),
            idempotencyKey: null,
            _mergeService,
            _auditWriter,
            _logger,
            TestContext.Current.CancellationToken);

        await _auditingWriter.DidNotReceive().WriteAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merge_StillReturnsOk_WhenAuditWriteFails()
    {
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal),
                DryRun: false));

        _auditingWriter.WriteAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Audit DB unreachable."));

        Results<Ok<PartyMergeResponse>, ProblemHttpResult, ValidationProblem> result = await PartyMergeEndpoints.HandleMergeAsync(
            Guid.NewGuid(),
            new PartyMergeRequest(LoserId: Guid.NewGuid()),
            idempotencyKey: null,
            _mergeService,
            _auditWriter,
            _logger,
            TestContext.Current.CancellationToken);

        // Merge already committed; audit failure must not surface to the caller.
        result.Result.ShouldBeOfType<Ok<PartyMergeResponse>>();
    }
}
