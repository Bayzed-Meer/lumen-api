using Lumen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumen.Infrastructure.Data.Configurations;

public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.UserId)
            .IsRequired();

        builder.Property(f => f.InstitutionalId)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(f => f.UserId).IsUnique();
        builder.HasIndex(f => f.InstitutionalId).IsUnique();

        builder.HasOne<Lumen.Infrastructure.Identity.ApplicationUser>()
            .WithOne()
            .HasForeignKey<Faculty>(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
