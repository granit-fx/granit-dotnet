using System.Text.Json;
using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Internal;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Granit.Parties.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for the duplicate-candidates review API. Reads + dismisses rows in
/// <c>parties_duplicate_candidates</c> (#1300) and provides a one-click "merge from
/// candidate" shortcut that resolves the row to (survivor, loser) and forwards to the
/// generic merge orchestrator (#1291).
/// </summary>
internal static partial class PartyDuplicatesEndpoints
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error,
        Message = "Failed to write audit entry for duplicate-shortcut merge: candidate={CandidateRowId}, survivor={SurvivorId}, loser={LoserId}.")]
    private static partial void LogAuditWriteFailed(
        ILogger logger, Guid candidateRowId, Guid survivorId, Guid loserId, Exception exception);

    /// <summary>Handler for <c>GET /parties/{id}/duplicate-candidates</c>.</summary>
    public static async Task<Ok<IReadOnlyList<PartyDuplicateCandidateResponse>>> HandleListForPartyAsync(
        Guid id,
        [FromServices] IPartyDuplicateCandidateStore store,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PartyDuplicateCandidate> rows =
            await store.ListForPartyAsync(id, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PartyDuplicateCandidateResponse> mapped =
            [.. rows.Select(MapResponse)];

        return TypedResults.Ok(mapped);
    }

    /// <summary>Handler for <c>POST /parties/duplicates/{id}/dismiss</c>. Idempotent.</summary>
    public static async Task<Results<NoContent, NotFound>> HandleDismissAsync(
        Guid id,
        [FromServices] IPartyDuplicateCandidateStore store,
        CancellationToken cancellationToken)
    {
        bool dismissed = await store.DismissAsync(id, cancellationToken).ConfigureAwait(false);
        // false either when the row does not exist OR it was already dismissed —
        // the second case is also idempotent-OK from the caller's perspective.
        // We return 404 only when the row truly doesn't exist; the store's contract
        // says "false when not found OR already dismissed", so we do an extra lookup
        // for that disambiguation.
        if (!dismissed)
        {
            PartyDuplicateCandidate? row = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return TypedResults.NotFound();
            }
        }
        return TypedResults.NoContent();
    }

    /// <summary>Handler for <c>POST /parties/duplicates/{id}/merge</c>.</summary>
    public static async Task<Results<Ok<PartyMergeResponse>, NotFound, ProblemHttpResult, ValidationProblem>> HandleMergeShortcutAsync(
        Guid id,
        [FromBody] PartyDuplicateMergeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] IPartyDuplicateCandidateStore store,
        [FromServices] IMergeService<Party> mergeService,
        [FromServices] PartyMergeAuditWriter auditWriter,
        [FromServices] ILogger<PartyMergeAuditWriterMarker> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        PartyDuplicateCandidate? row = await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        // Resolve which end of the candidate pair is the survivor / loser.
        Guid loserId;
        if (request.SurvivorId == row.PartyId)
        {
            loserId = row.CandidateId;
        }
        else if (request.SurvivorId == row.CandidateId)
        {
            loserId = row.PartyId;
        }
        else
        {
            return TypedResults.Problem(
                "survivorId is not part of the candidate pair on the supplied id.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        try
        {
            MergeFieldChoices choices = MergeFieldChoicesMapper.FromDictionary(request.Choices);
            MergeRequest mergeRequest = new(
                SurvivorId: request.SurvivorId,
                LoserId: loserId,
                Choices: choices,
                DryRun: false,
                Reason: request.Reason,
                IdempotencyKey: idempotencyKey);

            MergeResult<Party> result = await mergeService
                .MergeAsync(mergeRequest, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await auditWriter.RecordAsync(
                    survivorId: request.SurvivorId,
                    loserId: loserId,
                    resolvedChoices: request.Choices,
                    rewriteCounts: result.RewriteCounts,
                    reason: request.Reason,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogAuditWriteFailed(logger, id, request.SurvivorId, loserId, ex);
            }

            return TypedResults.Ok(new PartyMergeResponse(
                SurvivorId: request.SurvivorId,
                LoserId: loserId,
                Conflicts: [.. result.Conflicts.Select(c => new FieldConflictResponse(
                    c.FieldPath, c.SurvivorValue?.ToString(), c.LoserValue?.ToString(), c.Default.ToString()))],
                RewriteCounts: result.RewriteCounts,
                DryRun: result.DryRun));
        }
        catch (MergeException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOperationException ex)
        {
            // Idempotency-key conflict / already-merged / concurrent — surfaced from EfMergeService.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static PartyDuplicateCandidateResponse MapResponse(PartyDuplicateCandidate row)
    {
        IReadOnlyList<DuplicateMatchSignalResponse> signals = ParseSignals(row.SignalsJson);
        return new PartyDuplicateCandidateResponse(
            Id: row.Id,
            PartyId: row.PartyId,
            CandidateId: row.CandidateId,
            Score: row.Score,
            Tier: ((DuplicateMatchTier)row.Tier).ToString(),
            Signals: signals,
            DismissedAt: row.DismissedAt,
            CreatedAt: row.CreatedAt,
            UpdatedAt: row.UpdatedAt);
    }

    private static IReadOnlyList<DuplicateMatchSignalResponse> ParseSignals(string signalsJson)
    {
        if (string.IsNullOrWhiteSpace(signalsJson))
        {
            return [];
        }
        try
        {
            MatchSignal[]? raw = JsonSerializer.Deserialize<MatchSignal[]>(signalsJson);
            if (raw is null)
            {
                return [];
            }
            return [.. raw
                .OrderByDescending(s => s.Score)
                .Select(s => new DuplicateMatchSignalResponse(s.Kind, s.Score))];
        }
        catch (JsonException)
        {
            // Forward-compat: if the stored shape ever drifts, drop the signals rather
            // than fail the whole listing query.
            return [];
        }
    }

}

/// <summary>
/// Marker type used solely for the strongly-typed <c>ILogger&lt;T&gt;</c> dependency in
/// <see cref="PartyDuplicatesEndpoints.HandleMergeShortcutAsync"/>. Lives in the same file
/// so its scope is obvious.
/// </summary>
internal sealed class PartyMergeAuditWriterMarker;
