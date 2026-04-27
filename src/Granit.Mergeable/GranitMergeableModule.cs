using Granit.Modularity;

namespace Granit.Mergeable;

/// <summary>
/// Granit module exposing the generic merge primitive: <see cref="IMergeable{TSelf}"/>,
/// <see cref="IReferenceRewriter{TAggregate}"/> registry, <see cref="IMergeService{TAggregate}"/>
/// orchestrator contract, plus tombstone state (<c>Granit.Mergeable.Domain.IHasMergeTombstone</c>).
/// </summary>
/// <remarks>
/// This package is contract-only — register a concrete <c>IMergeService&lt;TAggregate&gt;</c>
/// (typically by depending on <c>Granit.Mergeable.EntityFrameworkCore</c>) and per-module
/// <see cref="IReferenceRewriter{TAggregate}"/> implementations to make it operational.
/// </remarks>
public sealed class GranitMergeableModule : GranitModule;
