using Granit.Modularity;

namespace Granit.DataLookup;

/// <summary>
/// Granit module for data lookup contracts (<see cref="Granit.DataLookup.Descriptors.LookupDescriptor"/>,
/// <see cref="Granit.DataLookup.Sources.ILookupSource"/>, <see cref="Granit.DataLookup.Registry.ILookupRegistry"/>).
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so that consumer modules
/// can declare <c>[DependsOn(typeof(GranitDataLookupAbstractionsModule))]</c>
/// without pulling in the full lookup runtime.
/// </remarks>
public sealed class GranitDataLookupAbstractionsModule : GranitModule;
