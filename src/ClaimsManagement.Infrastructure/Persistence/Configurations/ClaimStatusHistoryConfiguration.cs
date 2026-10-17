using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsManagement.Infrastructure.Persistence.Configurations;

public class ClaimStatusHistoryConfiguration : IEntityTypeConfiguration<ClaimStatusHistory>
{
    public void Configure(EntityTypeBuilder<ClaimStatusHistory> builder)
    {
        builder.ToTable("ClaimStatusHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(h => h.Note).HasMaxLength(500);
    }
}
