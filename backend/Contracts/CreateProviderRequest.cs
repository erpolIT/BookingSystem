using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Contracts;

public class CreateProviderRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}
