using BookingSystem.Models;

namespace BookingSystem.Services;

// Application outcomes; HTTP status codes and response bodies belong to the controller.
public abstract record CreateBookingResult
{
    public sealed record Created(Booking Booking) : CreateBookingResult;
    public sealed record AlreadyProcessed(Booking Booking) : CreateBookingResult;
    public sealed record InvalidCustomerName : CreateBookingResult;
    public sealed record IdempotencyKeyReused : CreateBookingResult;
    public sealed record ServiceNotFound : CreateBookingResult;
    public sealed record SlotUnavailable : CreateBookingResult;
    public sealed record SlotJustBooked : CreateBookingResult;
}
