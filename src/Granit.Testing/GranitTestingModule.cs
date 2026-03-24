using Granit.Guids;
using Granit.Modularity;
using Granit.Users;

namespace Granit.Testing;

/// <summary>
/// Granit module for shared test infrastructure.
/// </summary>
/// <remarks>
/// Provides <see cref="GranitTestFixture{TModule}"/>, configurable fakes
/// (<see cref="Fakes.FakeCurrentTenant"/>, <see cref="Fakes.FakeCurrentUser"/>,
/// <see cref="Fakes.FakeClock"/>, <see cref="Fakes.FakeGuidGenerator"/>),
/// and Bogus generators for Granit domain types.
/// This is a utility module with no runtime service registration.
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule))]
public sealed class GranitTestingModule : GranitModule;
