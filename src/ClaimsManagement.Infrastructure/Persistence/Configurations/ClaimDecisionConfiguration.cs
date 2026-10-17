using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsManagement.Infrastructure.Persistence.Configurations;

public class ClaimDecisionConfiguration : IEntityTypeConfiguration<ClaimDecision>
{
    public void Configure(EntityTypeBuilder<ClaimDecision> builder)
    {
        builder.ToTable("ClaimDecisions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.SettlementAmount).HasPrecision(18, 2);
        builder.Property(d => d.Reason).IsRequired().HasMaxLength(1000);

        builder.HasIndex(d => d.ClaimId).IsUnique();
    }
}
