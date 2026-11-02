using BookingSystem.Contracts;
using BookingSystem.Data;
using BookingSystem.Models;
using BookingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BookingSystem.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly BookingService _bookings;

    public BookingsController(BookingDbContext dbContext, BookingService bookings)
    {
        _dbContext = dbContext;
        _bookings = bookings;
    }

    [HttpGet]
    public async Task<ActionResult<List<Booking>>> GetMyBookings(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _dbContext.Bookings.AsNoTracking()
            .Where(booking => booking.UserId == userId)
            .OrderByDescending(booking => booking.StartTime).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Booking>> GetBooking(int id, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.Id == id && booking.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        if (booking is null) return NotFound();
        return Ok(booking);
    }

    [HttpPost]
    public async Task<ActionResult<Booking>> CreateBooking(CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null || idempotencyKey == Guid.Empty)
            return BadRequest(new { code = "invalid_idempotency_key", message = "A non-empty UUID Idempotency-Key header is required." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _bookings.CreateAsync(new CreateBookingCommand(userId, request.ServiceId,
            request.StartTime!.Value, request.CustomerName, request.CustomerEmail, idempotencyKey.Value),
            cancellationToken);

        switch (result)
        {
            case CreateBookingResult.Created created:
                return CreatedAtAction(nameof(GetBooking), new { id = created.Booking.Id }, created.Booking);
            case CreateBookingResult.AlreadyProcessed replay:
                return Ok(replay.Booking);
            case CreateBookingResult.InvalidCustomerName:
                ModelState.AddModelError(nameof(request.CustomerName), "CustomerName must contain at least two non-whitespace characters.");
                return ValidationProblem(ModelState);
            case CreateBookingResult.IdempotencyKeyReused:
                return Conflict(new { code = "idempotency_key_reused", message = "This key was used with different booking data." });
            case CreateBookingResult.ServiceNotFound:
                return NotFound("Service does not exist.");
            case CreateBookingResult.SlotUnavailable:
                return Conflict(new { code = "slot_unavailable", message = "The requested time is unavailable or outside the booking schedule." });
            case CreateBookingResult.SlotJustBooked:
                return Conflict(new { code = "slot_unavailable", message = "This time was just booked. Choose another slot." });
            default:
                throw new InvalidOperationException("Unknown booking creation outcome.");
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int id, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(booking => booking.Id == id && booking.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        if (booking is null) return NotFound();
        booking.Status = BookingStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
