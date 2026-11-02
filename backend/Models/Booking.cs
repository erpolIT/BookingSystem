using System.Text.Json.Serialization;

namespace BookingSystem.Models;

public enum BookingStatus { Confirmed, Cancelled }

public class Booking
{
    public int Id { get; set; }
    [JsonIgnore]
    public string? UserId { get; set; }
    [JsonIgnore]
    public Guid? IdempotencyKey { get; set; }
    [JsonIgnore]
    public string? RequestHash { get; set; }
    public int ServiceId { get; set; }
    public int ProviderId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    // Null means the price was not recorded for a legacy booking.
    public decimal? PriceAtBooking { get; set; }
    public BookingStatus Status { get; set; }
    public required string CustomerName { get; set; }
    public required string CustomerEmail { get; set; }
}
