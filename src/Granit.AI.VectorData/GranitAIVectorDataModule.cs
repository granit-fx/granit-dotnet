using Granit.AI;
using Granit.Modularity;

namespace Granit.AI.VectorData;

/// <summary>
/// Granit module for multi-tenant vector storage abstractions.
/// </summary>
/// <remarks>
/// Defines the <see cref="IVectorCollection{TRecord}"/>, <see cref="IVectorCollectionFactory"/>,
/// and <see cref="ISemanticSearchService"/> abstractions.
/// Register a concrete provider (e.g. <c>Granit.AI.VectorData.PgVector</c>) alongside this module.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIVectorDataModule : GranitModule;
