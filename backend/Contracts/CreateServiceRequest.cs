using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Contracts;

public class CreateServiceRequest
{
    [Range(1, int.MaxValue)]
    public int ProviderId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 480)]
    public int DurationMinutes { get; set; }

    [Range(typeof(decimal), "0.01", "10000", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }
}
