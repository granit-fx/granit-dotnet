using System.Text;
using System.Text.Json;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Keys;
using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Templating.Endpoints.Internal;

/// <summary>
/// Shared mapping helpers for template domain objects → response DTOs.
/// </summary>
internal static class TemplatingResponseMapper
{
    // -------------------------------------------------------------------------
    // Revision mapping
    // -------------------------------------------------------------------------

    public static TemplateRevisionResponse ToRevisionResponse(TemplateRevision revision) =>
        new(
            revision.RevisionId,
            revision.Content,
            revision.MimeType,
            revision.Status,
            revision.CreatedAt,
            revision.CreatedBy,
            revision.PublishedAt,
            revision.PublishedBy,
            revision.LayoutName,
            revision.ConcurrencyStamp);

    // -------------------------------------------------------------------------
    // Category mapping
    // -------------------------------------------------------------------------

    public static TemplateCategoryResponse ToCategoryResponse(TemplateCategory category) =>
        new(category.Id, category.Name, category.Description, category.Icon,
            category.SortOrder, category.TemplateCount);

    // -------------------------------------------------------------------------
    // Detail response builder (reused by Create, Update, Publish)
    // -------------------------------------------------------------------------

    public static async Task<TemplateDetailResponse> BuildDetailResponseAsync(
        IDocumentTemplateStoreReader storeReader,
        TemplateKey key,
        CancellationToken cancellationToken)
    {
        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == WorkflowLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = ToRevisionResponse(publishedRevision);
            }
        }

        return new TemplateDetailResponse(
            key.Name,
            key.Culture,
            draft is not null ? ToRevisionResponse(draft) : null,
            publishedResponse,
            draft?.LayoutName ?? published?.LayoutName);
    }

    // -------------------------------------------------------------------------
    // Guard helpers
    // -------------------------------------------------------------------------

    public static ProblemHttpResult StoreNotRegistered() =>
        TypedResults.Problem(
            detail: "No template store is registered. Install a persistence module to enable this feature.",
            statusCode: StatusCodes.Status501NotImplemented);

    public static ProblemHttpResult? ValidateTemplateName(string name)
    {
        if (name.Length > TemplatingPatterns.MaxNameLength)
        {
            return TypedResults.Problem(
                detail: $"Template name must not exceed {TemplatingPatterns.MaxNameLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TemplatingPatterns.TemplateNamePattern().IsMatch(name))
        {
            return TypedResults.Problem(
                detail: "Template name must follow the 'Domain.Name' pattern (e.g. 'Billing.Invoice').",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    public static ProblemHttpResult? ValidateBcp47(string cultureName) =>
        TemplatingPatterns.Bcp47Pattern().IsMatch(cultureName)
            ? null
            : TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not a valid BCP 47 tag.",
                statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult? ValidatePagination(int page, int pageSize)
    {
        if (page is < 1 or > 10_000)
        {
            return TypedResults.Problem(
                detail: "Page must be between 1 and 10000.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (pageSize is < 1 or > 100)
        {
            return TypedResults.Problem(
                detail: "PageSize must be between 1 and 100.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Variable introspection helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a PascalCase property name to snake_case.
    /// Replicates Scriban's <c>StandardMemberRenamer.Default</c> behavior.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        StringBuilder sb = new();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static string MapClrTypeName(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return "string";
        }

        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(short) || underlying == typeof(byte) ||
            underlying == typeof(decimal) || underlying == typeof(double) ||
            underlying == typeof(float))
        {
            return "number";
        }

        if (underlying == typeof(bool))
        {
            return "boolean";
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) ||
            underlying == typeof(DateOnly))
        {
            return "date";
        }

        if (underlying == typeof(TimeOnly) || underlying == typeof(TimeSpan))
        {
            return "time";
        }

        if (underlying == typeof(Guid))
        {
            return "string";
        }

        return "object";
    }

    // -------------------------------------------------------------------------
    // JSON → dictionary conversion (preview data model)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a <see cref="JsonElement"/> object to a <see cref="Dictionary{TKey, TValue}"/>
    /// suitable for template engine rendering.
    /// </summary>
    public static Dictionary<string, object?> ConvertJsonObject(JsonElement element)
    {
        Dictionary<string, object?> dict = [];

        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonValue(property.Value);
        }

        return dict;
    }

    public static object? ConvertJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
}
