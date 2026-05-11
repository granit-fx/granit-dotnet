using System.Text.Json;

namespace Granit.Entities.Endpoints.Dtos.BulkActions;

/// <summary>
/// Client request for a bulk action invocation. The IDs target specific entities;
/// the payload contains action-specific parameters.
/// </summary>
/// <param name="Ids">
/// Primary keys (GUIDs or ints rendered as strings) of all entities to be
/// affected by this bulk action. Must not be empty.
/// </param>
/// <param name="Payload">
/// Optional action-specific parameters. Passed verbatim to the executor as a
/// <see cref="JsonElement"/>. Interpreting its structure is the executor's
/// responsibility. Null is coalesced to <c>{}</c> by the endpoint.
/// </param>
public sealed record BulkActionRequest(
    IReadOnlyList<string> Ids,
    JsonElement? Payload = null);
