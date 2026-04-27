using Granit.Auditing;
using Granit.Guids;
using Granit.Mergeable;
using Granit.MultiTenancy;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Endpoints;
using Granit.Parties.Endpoints.Internal;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Endpoints;

/// <summary>
/// Unit tests for <see cref="PartyDuplicatesEndpoints"/>. Substitutes the store + merge
/// service and validates the HTTP-shape contract.
/// </summary>
public sealed class PartyDuplicatesEndpointsTests
{
    private readonly IPartyDuplicateCandidateStore _store = Substitute.For<IPartyDuplicateCandidateStore>();
    private readonly IMergeService<Party> _mergeService = Substitute.For<IMergeService<Party>>();
    private readonly PartyMergeAuditWriter _auditWriter;

    public PartyDuplicatesEndpointsTests()
    {
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("admin-1");
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);
        IAuditingWriter auditingWriter = Substitute.For<IAuditingWriter>();

        _auditWriter = new PartyMergeAuditWriter(auditingWriter, user, tenant, guidGen, clock);
    }

    [Fact]
    public async Task HandleDismissAsync_returns_204_when_row_dismissed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        _store.DismissAsync(rowId, ct).Returns(true);

        Results<NoContent, NotFound> response = await PartyDuplicatesEndpoints.HandleDismissAsync(
            rowId, _store, ct);

        response.Result.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public async Task HandleDismissAsync_returns_204_when_already_dismissed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        _store.DismissAsync(rowId, ct).Returns(false);
        var existing = PartyDuplicateCandidate.Create(
            id: rowId, tenantId: null,
            partyId: new("11111111-1111-1111-1111-111111111111"),
            candidateId: new("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            tier: (int)DuplicateMatchTier.Blocking, score: 0.7m,
            signalsJson: "[]", createdAt: DateTimeOffset.UtcNow);
        existing.Dismiss(DateTimeOffset.UtcNow);
        _store.FindByIdAsync(rowId, ct).Returns(existing);

        Results<NoContent, NotFound> response = await PartyDuplicatesEndpoints.HandleDismissAsync(
            rowId, _store, ct);

        response.Result.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public async Task HandleDismissAsync_returns_404_when_row_not_found()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        _store.DismissAsync(rowId, ct).Returns(false);
        _store.FindByIdAsync(rowId, ct).Returns((PartyDuplicateCandidate?)null);

        Results<NoContent, NotFound> response = await PartyDuplicatesEndpoints.HandleDismissAsync(
            rowId, _store, ct);

        response.Result.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task HandleMergeShortcutAsync_returns_404_when_candidate_row_missing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        _store.FindByIdAsync(rowId, ct).Returns((PartyDuplicateCandidate?)null);

        Results<Ok<PartyMergeResponse>, NotFound, ProblemHttpResult, ValidationProblem> response = await PartyDuplicatesEndpoints.HandleMergeShortcutAsync(
            id: rowId,
            request: new PartyDuplicateMergeRequest(SurvivorId: Guid.NewGuid()),
            idempotencyKey: null,
            store: _store, mergeService: _mergeService, auditWriter: _auditWriter,
            logger: NullLogger<PartyMergeAuditWriterMarker>.Instance,
            cancellationToken: ct);

        response.Result.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task HandleMergeShortcutAsync_returns_422_when_survivorId_not_in_pair()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        var lower = new Guid("11111111-1111-1111-1111-111111111111");
        var higher = new Guid("22222222-2222-2222-2222-222222222222");

        var row = PartyDuplicateCandidate.Create(
            id: rowId, tenantId: null, partyId: lower, candidateId: higher,
            tier: (int)DuplicateMatchTier.Blocking, score: 0.8m,
            signalsJson: "[]", createdAt: DateTimeOffset.UtcNow);
        _store.FindByIdAsync(rowId, ct).Returns(row);

        Results<Ok<PartyMergeResponse>, NotFound, ProblemHttpResult, ValidationProblem> response = await PartyDuplicatesEndpoints.HandleMergeShortcutAsync(
            id: rowId,
            request: new PartyDuplicateMergeRequest(SurvivorId: Guid.NewGuid()), // unrelated
            idempotencyKey: null,
            store: _store, mergeService: _mergeService, auditWriter: _auditWriter,
            logger: NullLogger<PartyMergeAuditWriterMarker>.Instance,
            cancellationToken: ct);

        ProblemHttpResult problem = response.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(422);
    }

    [Fact]
    public async Task HandleMergeShortcutAsync_resolves_loserId_from_pair_and_calls_orchestrator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var rowId = Guid.NewGuid();
        var lower = new Guid("11111111-1111-1111-1111-111111111111");
        var higher = new Guid("22222222-2222-2222-2222-222222222222");

        var row = PartyDuplicateCandidate.Create(
            id: rowId, tenantId: null, partyId: lower, candidateId: higher,
            tier: (int)DuplicateMatchTier.Blocking, score: 0.8m,
            signalsJson: "[]", createdAt: DateTimeOffset.UtcNow);
        _store.FindByIdAsync(rowId, ct).Returns(row);

        // Survivor = lower → orchestrator should receive (lower, higher).
        _mergeService.MergeAsync(Arg.Any<MergeRequest>(), ct)
            .Returns(new MergeResult<Party>(
                Merged: null,
                Conflicts: [],
                RewriteCounts: new Dictionary<string, int>(),
                DryRun: false));

        Results<Ok<PartyMergeResponse>, NotFound, ProblemHttpResult, ValidationProblem> response = await PartyDuplicatesEndpoints.HandleMergeShortcutAsync(
            id: rowId,
            request: new PartyDuplicateMergeRequest(SurvivorId: lower),
            idempotencyKey: "key-1",
            store: _store, mergeService: _mergeService, auditWriter: _auditWriter,
            logger: NullLogger<PartyMergeAuditWriterMarker>.Instance,
            cancellationToken: ct);

        Ok<PartyMergeResponse> ok = response.Result.ShouldBeOfType<Ok<PartyMergeResponse>>();
        ok.Value!.SurvivorId.ShouldBe(lower);
        ok.Value!.LoserId.ShouldBe(higher);

        // Orchestrator was called with the resolved (survivor, loser) pair + the idem key.
        await _mergeService.Received(1).MergeAsync(
            Arg.Is<MergeRequest>(r =>
                r.SurvivorId == lower && r.LoserId == higher && r.IdempotencyKey == "key-1"),
            ct);
    }
}
