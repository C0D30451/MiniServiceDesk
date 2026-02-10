using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.Dtos;

public sealed class CreateUserRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(80)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(120)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(User|Agent|Admin)$")]
    public string Role { get; init; } = "User";
}
