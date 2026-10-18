using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Controllers;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/admin/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminBookingsController(BookingDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery, Required] DateOnly? date,
        [FromQuery] int? providerId, CancellationToken cancellationToken)
    {
        if ((date!.Value == DateOnly.MinValue || date.Value == DateOnly.MaxValue) || providerId is <= 0)
            return BadRequest(new { message = "Choose a valid date and provider." });
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.Value.ToDateTime(TimeOnly.MinValue), zone));
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
        var query = db.Bookings.AsNoTracking().Where(b => b.StartTime >= start && b.StartTime < end);
        if (providerId.HasValue) query = query.Where(b => b.ProviderId == providerId.Value);
        var rows = await (from booking in query
            join provider in db.Providers on booking.ProviderId equals provider.Id
            join service in db.Services on booking.ServiceId equals service.Id
            orderby booking.StartTime, booking.Id
            select new { booking.Id, booking.ServiceId, booking.ProviderId, ProviderName = provider.Name,
                ServiceName = service.Name, booking.StartTime, booking.EndTime,
                booking.Status, booking.CustomerName, booking.CustomerEmail })
            .Take(501).ToListAsync(cancellationToken);
        return Ok(new { bookings = rows.Take(500), truncated = rows.Count > 500 });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        // Atomic and repeatable; does not expose the owner-only customer endpoint.
        var updated = await db.Bookings.Where(b => b.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Status, BookingStatus.Cancelled), cancellationToken);
        return updated == 0 ? NotFound() : NoContent();
    }
}
