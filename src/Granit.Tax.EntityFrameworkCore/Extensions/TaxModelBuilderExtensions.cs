using Granit.Tax.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Tax.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Tax entity configurations.</summary>
public static class TaxModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit.Tax module.</summary>
    public static ModelBuilder ConfigureTaxModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ValidatedTaxIdConfiguration());
        modelBuilder.ApplyConfiguration(new TaxRateOverrideConfiguration());
        return modelBuilder;
    }
}
