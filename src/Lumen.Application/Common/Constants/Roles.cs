using Lumen.Domain.Enums;

namespace Lumen.Application.Common.Constants;

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Librarian = nameof(UserRole.Librarian);
    public const string Student = nameof(UserRole.Student);
    public const string Faculty = nameof(UserRole.Faculty);
}
