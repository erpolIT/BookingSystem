using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Contracts;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }
    [Required]
    public DateTimeOffset? StartTime { get; set; }
    [Required, StringLength(100, MinimumLength = 2)]
    public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)]
    public string CustomerEmail { get; set; } = string.Empty;
}
