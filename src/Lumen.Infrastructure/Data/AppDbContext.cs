using Lumen.Domain.Entities;
using Lumen.Infrastructure.Entities;
using Lumen.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lumen.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Faculty> Faculty => Set<Faculty>();
    public DbSet<Librarian> Librarians => Set<Librarian>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
