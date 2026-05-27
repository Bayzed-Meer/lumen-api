using Lumen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumen.Infrastructure.Data.Configurations;

public sealed class OtpRecordConfiguration : IEntityTypeConfiguration<OtpRecord>
{
    public void Configure(EntityTypeBuilder<OtpRecord> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .IsRequired();

        builder.Property(o => o.CodeHash)
            .IsRequired();

        builder.HasIndex(o => o.UserId);

        builder.HasOne<Lumen.Infrastructure.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
