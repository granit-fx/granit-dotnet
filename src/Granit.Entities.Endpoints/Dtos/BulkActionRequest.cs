using System.Text.Json;

namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Request body for <c>POST /api/entities/{name}/bulk/{action}</c>.
/// </summary>
/// <param name="Ids">Primary keys of the rows to act upon. Must be non-empty.</param>
/// <param name="Payload">Free-form JSON payload forwarded verbatim to the registered <c>IEntityActionExecutor&lt;TEntity&gt;</c>. Pass an empty object <c>{}</c> when the action takes no parameters.</param>
public sealed record BulkActionRequest(IReadOnlyList<Guid> Ids, JsonElement Payload);
