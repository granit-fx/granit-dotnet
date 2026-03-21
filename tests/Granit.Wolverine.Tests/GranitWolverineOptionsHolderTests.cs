// =============================================================================
// Tests - GranitWolverineOptionsHolder
// =============================================================================
// Verifies the internal marker class that holds the captured WolverineOptions.
// =============================================================================

using Granit.Wolverine.Internal;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class GranitWolverineOptionsHolderTests
{
    [Fact]
    public void Constructor_StoresOptions()
    {
        WolverineOptions options = new();

        GranitWolverineOptionsHolder holder = new(options);

        holder.Options.ShouldBeSameAs(options);
    }
}
