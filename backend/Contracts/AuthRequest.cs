using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Contracts;

public class AuthRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;
}
