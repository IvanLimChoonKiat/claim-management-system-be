using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsManagement.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        // App-generated Guid, not DB-generated - without this EF's default "ValueGeneratedOnAdd"
        // convention for Guid keys can misidentify a new entity as already-persisted (see Claim.Id).
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
