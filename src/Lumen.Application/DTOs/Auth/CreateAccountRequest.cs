using System.ComponentModel.DataAnnotations;
using Lumen.Application.Common.Constants;

namespace Lumen.Application.DTOs.Auth;

public sealed record CreateAccountRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [MaxLength(100)]
    public required string FirstName { get; init; }

    [Required]
    [MaxLength(100)]
    public required string LastName { get; init; }

    [Required]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
    public required string Password { get; init; }

    [Required]
    [RegularExpression($"^({Roles.Student}|{Roles.Faculty}|{Roles.Librarian})$",
        ErrorMessage = $"Role must be one of: {Roles.Student}, {Roles.Faculty}, {Roles.Librarian}.")]
    public required string Role { get; init; }

    [Required]
    [MaxLength(50)]
    public required string InstitutionalId { get; init; }
}
