using Lumen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumen.Infrastructure.Data.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.InstitutionalId)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.UserId).IsUnique();
        builder.HasIndex(s => s.InstitutionalId).IsUnique();

        builder.HasOne<Lumen.Infrastructure.Identity.ApplicationUser>()
            .WithOne()
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
