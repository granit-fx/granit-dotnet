using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.Parties.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for the party merge admin API. Routes the admin endpoint to the
/// generic merge orchestrator (<see cref="IMergeService{Party}"/>) and shapes the
/// response into a wire-friendly DTO.
/// </summary>
internal static class PartyMergeEndpoints
{
    /// <summary>
    /// Handler for <c>GET /parties/{survivorId}/merge/preview?loserId=&lt;guid&gt;</c>.
    /// Always a dry-run — computes per-field conflicts (with default <c>WinnerSide</c>
    /// pre-populated) and per-rewriter row counts without committing anything. Powers
    /// the admin merge wizard's side-by-side view.
    /// </summary>
    public static async Task<Results<Ok<PartyMergeResponse>, ProblemHttpResult>> HandlePreviewAsync(
        Guid survivorId,
        [FromQuery] Guid loserId,
        [FromServices] IMergeService<Party> mergeService,
        CancellationToken cancellationToken)
    {
        try
        {
            MergeResult<Party> result = await mergeService
                .MergePreviewAsync(survivorId, loserId, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(MapResponse(survivorId, loserId, result));
        }
        catch (MergeException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    /// <summary>
    /// Handler for <c>POST /parties/{survivorId}/merge</c>. Runs the live merge (or a
    /// dry-run if <see cref="PartyMergeRequest.DryRun"/> is set) inside the orchestrator's
    /// <see cref="System.Transactions.TransactionScope"/>. Stripe-style idempotency: when
    /// an <c>Idempotency-Key</c> header is present, repeats with the same payload return
    /// the cached result; repeats with a different payload return 409.
    /// </summary>
    /// <remarks>
    /// HTTP status mapping:
    /// <list type="bullet">
    /// <item><c>422</c> — domain invariant violated (tenant / kind / currency mismatch,
    /// archived loser, …) → <see cref="MergeException"/>.</item>
    /// <item><c>409</c> — concurrent merge raced ahead, or idempotency key reused with a
    /// different payload → <see cref="InvalidOperationException"/> from the orchestrator.</item>
    /// <item><c>404</c> — survivor or loser not found.</item>
    /// </list>
    /// </remarks>
    public static async Task<Results<Ok<PartyMergeResponse>, ProblemHttpResult, ValidationProblem>> HandleMergeAsync(
        Guid survivorId,
        PartyMergeRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] IMergeService<Party> mergeService,
        CancellationToken cancellationToken)
    {
        var orchestratorRequest = new MergeRequest(
            SurvivorId: survivorId,
            LoserId: request.LoserId,
            Choices: BuildChoices(request.Choices),
            DryRun: request.DryRun,
            Reason: request.Reason,
            IdempotencyKey: idempotencyKey);

        try
        {
            MergeResult<Party> result = await mergeService
                .MergeAsync(orchestratorRequest, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(MapResponse(survivorId, request.LoserId, result));
        }
        catch (MergeException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOperationException ex)
        {
            // The orchestrator throws InvalidOperationException for: idempotency-key conflicts
            // (same key, different payload), already-merged loser, optimistic-concurrency
            // races. All map cleanly to 409.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static MergeFieldChoices BuildChoices(IReadOnlyDictionary<string, string>? wire)
    {
        if (wire is null || wire.Count == 0)
        {
            return MergeFieldChoices.Empty;
        }

        Dictionary<string, WinnerSide> mapped = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in wire)
        {
            // Validator already enforces "Survivor" / "Loser" — Enum.Parse here covers the
            // happy path. An invalid value would have been rejected before reaching the
            // handler, so a defensive fallback isn't necessary.
            mapped[kv.Key] = Enum.Parse<WinnerSide>(kv.Value);
        }
        return new MergeFieldChoices(mapped);
    }

    private static PartyMergeResponse MapResponse(Guid survivorId, Guid loserId, MergeResult<Party> result) =>
        new(
            SurvivorId: survivorId,
            LoserId: loserId,
            Conflicts: result.Conflicts.Select(c => new FieldConflictResponse(
                FieldPath: c.FieldPath,
                SurvivorValue: c.SurvivorValue?.ToString(),
                LoserValue: c.LoserValue?.ToString(),
                Default: c.Default.ToString())).ToList(),
            RewriteCounts: result.RewriteCounts,
            DryRun: result.DryRun);
}
