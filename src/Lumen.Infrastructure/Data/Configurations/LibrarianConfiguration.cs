using Lumen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumen.Infrastructure.Data.Configurations;

public sealed class LibrarianConfiguration : IEntityTypeConfiguration<Librarian>
{
    public void Configure(EntityTypeBuilder<Librarian> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId)
            .IsRequired();

        builder.Property(l => l.InstitutionalId)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(l => l.UserId).IsUnique();
        builder.HasIndex(l => l.InstitutionalId).IsUnique();

        builder.HasOne<Lumen.Infrastructure.Identity.ApplicationUser>()
            .WithOne()
            .HasForeignKey<Librarian>(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
