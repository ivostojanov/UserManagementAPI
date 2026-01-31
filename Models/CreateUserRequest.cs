using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

public record CreateUserRequest(
    [property: Required(AllowEmptyStrings = false)] string Name,
    [property: EmailAddress] string? Email
);
