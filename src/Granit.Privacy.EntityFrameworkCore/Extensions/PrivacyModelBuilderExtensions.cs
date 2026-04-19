using Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Privacy entity configurations.</summary>
public static class PrivacyModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Privacy module.</summary>
    public static ModelBuilder ConfigurePrivacyModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LegalDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ExportRequestEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeletionRequestEntityConfiguration());
        return modelBuilder;
    }
}
