using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Collects every tag referenced by an operation, sorts them alphabetically,
/// and writes them to <see cref="OpenApiDocument.Tags"/>. Without this, Scalar and
/// other UIs render tags in the order operations were registered — i.e. module
/// load order, which is not meaningful to API consumers.
/// </summary>
internal sealed class SortedTagsDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        HashSet<string> names = [];
        foreach (OpenApiPathItem path in document.Paths.Values)
        {
            if (path.Operations is null)
            {
                continue;
            }

            foreach (OpenApiOperation operation in path.Operations.Values)
            {
                if (operation.Tags is null)
                {
                    continue;
                }

                foreach (OpenApiTagReference reference in operation.Tags)
                {
                    if (!string.IsNullOrEmpty(reference.Name))
                    {
                        names.Add(reference.Name);
                    }
                }
            }
        }

        if (names.Count == 0)
        {
            return Task.CompletedTask;
        }

        // SortedSet preserves order during serialization; OpenApiTag has no
        // IComparable, so we pass a name-based comparer.
        SortedSet<OpenApiTag> sorted = new(OpenApiTagNameComparer.Instance);
        foreach (string name in names)
        {
            sorted.Add(new OpenApiTag { Name = name });
        }

        document.Tags = sorted;
        return Task.CompletedTask;
    }

    private sealed class OpenApiTagNameComparer : IComparer<OpenApiTag>
    {
        public static readonly OpenApiTagNameComparer Instance = new();

        public int Compare(OpenApiTag? x, OpenApiTag? y) =>
            string.Compare(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
    }
}
