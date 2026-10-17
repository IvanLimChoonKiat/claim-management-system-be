using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsManagement.Infrastructure.Persistence.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.Id);
        // App-generated Guid, not DB-generated - see docs/ARCHITECTURE.md / UserConfiguration for why
        // this matters: without it, EF's "new vs existing" heuristic for children added post-load
        // (e.g. a new ClaimStatusHistory row on Assign()) can misfire and emit an UPDATE instead of
        // an INSERT, which then looks like a spurious concurrency conflict.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ClaimReference).IsRequired().HasMaxLength(32);
        builder.HasIndex(c => c.ClaimReference).IsUnique();

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.AssignedOfficerUserId);

        builder.Property(c => c.IncidentDescription).IsRequired().HasMaxLength(4000);
        builder.Property(c => c.IncidentLocation).IsRequired().HasMaxLength(500);
        builder.Property(c => c.VehicleRegistration).HasMaxLength(32);
        builder.Property(c => c.PropertyAddress).HasMaxLength(500);
        builder.Property(c => c.EstimatedLiability).HasPrecision(18, 2);

        // Optimistic concurrency token - SQLite has no native rowversion (docs/ARCHITECTURE.md).
        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasMany(c => c.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.InformationRequests)
            .WithOne()
            .HasForeignKey(r => r.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.InformationRequests).HasField("_informationRequests").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(c => c.Decision)
            .WithOne()
            .HasForeignKey<ClaimDecision>(d => d.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        // In-memory only - never persisted.
        builder.Ignore(c => c.DomainEvents);
    }
}
