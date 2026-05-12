using System.Text.Json;
using Granit.QueryEngine.AI.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using SharpFuzz;

// AFL++ persistent-mode harness around the post-LLM JSON deserialization path of
// LlmNaturalLanguageQueryTranslator. We do NOT fuzz the LLM call itself — fuzzing
// network/IO is out of scope. We fuzz the JSON-to-QueryRequest translation: this is
// the only attacker-influenced parse step in the AI module (an attacker who can
// jailbreak the LLM controls the JSON shape).
//
// Any unhandled exception escaping the call is a finding. JsonException is the
// documented happy-path failure mode — production catches it and returns null —
// and is filtered here.

const int MaxInputChars = 16 * 1024;

// Static metadata mirrors the test CreateTestMetadata fixture. It's the validation
// whitelist used by TryDeserializeAndConvert; we keep it diverse so the validator
// exercises both accept and reject paths.
QueryMetadata metadata = new()
{
    FilterableFields =
    [
        new FilterableField("status", "string", [FilterOperator.Eq, FilterOperator.Contains]),
        new FilterableField("amount", "decimal", [FilterOperator.Gte, FilterOperator.Lte]),
        new FilterableField("name", "string", [FilterOperator.Contains]),
    ],
    SortableFields =
    [
        new SortableField("name"),
        new SortableField("createdAt"),
        new SortableField("amount"),
    ],
    QuickFilters =
    [
        new QuickFilterMeta("active", "Active Items", false),
    ],
    DateFilters = [],
    GroupByFields =
    [
        new GroupByField("status", "string"),
    ],
    Columns = [],
    PresetFilterGroups = [],
    Pagination = new PaginationMeta(25, 100, 1000, false),
};

Fuzzer.Run((string raw) =>
{
    if (raw.Length == 0)
    {
        return;
    }

    string json = raw.Length > MaxInputChars ? raw[..MaxInputChars] : raw;

    try
    {
        _ = LlmNaturalLanguageQueryTranslator.TryDeserializeAndConvert(json, metadata, out _);
    }
    catch (JsonException)
    {
        // Documented happy-path failure mode — production swallows this.
    }
});
