using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsManagement.Infrastructure.Persistence.Configurations;

public class InformationRequestConfiguration : IEntityTypeConfiguration<InformationRequest>
{
    public void Configure(EntityTypeBuilder<InformationRequest> builder)
    {
        builder.ToTable("InformationRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.RequestText).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.ResponseText).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}
