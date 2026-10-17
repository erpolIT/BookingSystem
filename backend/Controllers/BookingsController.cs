using BookingSystem.Contracts;
using BookingSystem.Data;
using BookingSystem.Models;
using BookingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BookingSystem.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly AvailabilityService _availability;

    public BookingsController(BookingDbContext dbContext, AvailabilityService availability)
    {
        _dbContext = dbContext;
        _availability = availability;
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
        var name = request.CustomerName.Trim();
        if (name.Length < 2)
        {
            ModelState.AddModelError(nameof(request.CustomerName), "CustomerName must contain at least two non-whitespace characters.");
            return ValidationProblem(ModelState);
        }
        var start = request.StartTime!.Value.ToUniversalTime();
        var email = request.CustomerEmail.Trim();
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.ServiceId, StartTime = start, CustomerName = name, CustomerEmail = email }))));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize requests with the same key across all API instances.
        // The lock is released automatically when the transaction ends.
        var lockKey = BitConverter.ToInt64(SHA256.HashData(idempotencyKey.Value.ToByteArray()), 0);
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey});", cancellationToken);

        var existing = await _dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.IdempotencyKey == idempotencyKey && booking.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                return Conflict(new { code = "idempotency_key_reused", message = "This key was used with different booking data." });
            await transaction.CommitAsync(cancellationToken);
            return Ok(existing);
        }

        var service = await _dbContext.Services.AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == request.ServiceId, cancellationToken);
        if (service is null) return NotFound("Service does not exist.");

        var slots = await _availability.GetSlots(service, AvailabilityService.GetLocalDate(start), cancellationToken);
        if (!slots.Any(slot => slot.StartTime == start))
            return Conflict(new { code = "slot_unavailable", message = "The requested time is unavailable or outside the booking schedule." });

        var booking = new Booking
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            ServiceId = service.Id,
            ProviderId = service.ProviderId,
            StartTime = start,
            EndTime = start.AddMinutes(service.DurationMinutes),
            Status = BookingStatus.Confirmed,
            CustomerName = name,
            CustomerEmail = email
        };
        _dbContext.Bookings.Add(booking);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: "EX_Bookings_NoOverlap" })
        {
            return Conflict(new { code = "slot_unavailable", message = "This time was just booked. Choose another slot." });
        }
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
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
