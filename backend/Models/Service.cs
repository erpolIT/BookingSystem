namespace BookingSystem.Models;


public class Service
{
    public int ProviderId { get; set; }
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
}