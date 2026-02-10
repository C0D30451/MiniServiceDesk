using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.Dtos;

public sealed class UpdateUserRoleRequest
{
    [Required]
    [RegularExpression("^(User|Agent|Admin)$")]
    public string Role { get; init; } = "User";
}
