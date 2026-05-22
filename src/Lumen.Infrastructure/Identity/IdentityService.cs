using Lumen.Application.Common.Interfaces;
using Lumen.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace Lumen.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext) : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly AppDbContext _dbContext = dbContext;
}
