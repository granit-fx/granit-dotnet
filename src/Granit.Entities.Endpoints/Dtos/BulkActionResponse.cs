using Granit.Entities.Actions.Execution;

namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Response body for <c>POST /api/entities/{name}/bulk/{action}</c>.
/// </summary>
/// <param name="Affected">Number of rows the executor accepted.</param>
/// <param name="Failures">Per-row failures reported by the executor. Empty when every row succeeded.</param>
public sealed record BulkActionResponse(int Affected, IReadOnlyList<BulkActionFailure> Failures);
