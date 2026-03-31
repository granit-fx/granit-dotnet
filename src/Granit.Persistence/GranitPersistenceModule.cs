using Granit.Modularity;

namespace Granit.Persistence;

/// <summary>
/// Module for persistence-agnostic abstractions: <see cref="Specification{T}"/>,
/// <see cref="SortExpression{T}"/>.
/// </summary>
/// <remarks>
/// This module has no ORM dependency. EF Core, MongoDB, and Dapper implementations
/// depend on this module for shared abstractions.
/// </remarks>
public sealed class GranitPersistenceModule : GranitModule;
