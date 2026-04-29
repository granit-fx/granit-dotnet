using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.PostGIS.Internal;
using NetTopologySuite.Geometries;
using Shouldly;
using Xunit;

namespace Granit.Analytics.PostGIS.Tests;

/// <summary>
/// Locks the contract of <see cref="NtsGeographyPointProjector{TEntity}"/> —
/// the WKT axis-order convention (X = lng, Y = lat), the null-Point fallback,
/// and the typed error messages that surface when the host wires the projector
/// against an entity that doesn't carry a NetTopologySuite <see cref="Point"/>.
/// </summary>
public sealed class NtsGeographyPointProjectorTests
{
    [Fact]
    public void TryProject_PointPresent_ReturnsLatLng_HonouringWktAxisOrder()
    {
        // Paris: longitude 2.3522, latitude 48.8566. NTS Point uses (X=lng, Y=lat).
        var point = new Point(x: 2.3522, y: 48.8566);
        var entity = new Branch { Location = point };

        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();
        (double Latitude, double Longitude)? result = projector.TryProject(entity, "Location");

        result.ShouldNotBeNull();
        result!.Value.Latitude.ShouldBe(48.8566);
        result.Value.Longitude.ShouldBe(2.3522);
    }

    [Fact]
    public void TryProject_NullPoint_ReturnsNull()
    {
        var entity = new Branch { Location = null };

        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();
        (double Latitude, double Longitude)? result = projector.TryProject(entity, "Location");

        result.ShouldBeNull();
    }

    [Fact]
    public void TryProject_ColumnNameIsCaseInsensitive()
    {
        var point = new Point(2.0, 1.0);
        var entity = new Branch { Location = point };

        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();
        projector.TryProject(entity, "location").ShouldNotBeNull();
        projector.TryProject(entity, "LOCATION").ShouldNotBeNull();
    }

    [Fact]
    public void TryProject_UnknownColumn_Throws()
    {
        var entity = new Branch { Location = new Point(0, 0) };

        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();

        Should.Throw<ArgumentException>(() =>
            projector.TryProject(entity, "NotAColumn")).Message.ShouldContain("NotAColumn");
    }

    [Fact]
    public void TryProject_ColumnIsNotAPoint_Throws()
    {
        // The host wired the projector against a property that's not a NTS Point —
        // produce a typed error pointing at the misconfiguration, not a NRE.
        var entity = new Branch { Location = new Point(0, 0) };

        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();

        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            projector.TryProject(entity, "Name"));
        ex.Message.ShouldContain("Name");
        ex.Message.ShouldContain("Point");
    }

    [Fact]
    public void TryProject_NullEntity_Throws()
    {
        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();
        Should.Throw<ArgumentNullException>(() => projector.TryProject(null!, "Location"));
    }

    [Fact]
    public void TryProject_BlankColumn_Throws()
    {
        IGeographyPointProjector<Branch> projector = new NtsGeographyPointProjector<Branch>();
        Should.Throw<ArgumentException>(() => projector.TryProject(new Branch(), ""));
    }

    private sealed class Branch
    {
        public string Name { get; init; } = "Test";
        public Point? Location { get; init; }
    }
}
