using Microsoft.AspNetCore.Identity;

namespace Lumen.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
