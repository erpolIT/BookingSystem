using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Services;

public record AvailableSlot(DateTimeOffset StartTime, DateTimeOffset EndTime);

public class AvailabilityService
{
    private readonly BookingDbContext _dbContext;
    private readonly TimeProvider _clock;

    public AvailabilityService(BookingDbContext dbContext, TimeProvider clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public static DateOnly GetLocalDate(DateTimeOffset instant) => SlotGenerator.GetLocalDate(instant);

    public async Task<List<AvailableSlot>> GetSlots(Service service, DateOnly date, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (date < GetLocalDate(now) || date > GetLocalDate(now).AddYears(1)) return [];
        var hours = await _dbContext.WorkingHours.AsNoTracking().FirstOrDefaultAsync(
            hours => hours.ProviderId == service.ProviderId && hours.DayOfWeek == date.DayOfWeek,
            cancellationToken);
        if (hours is null) return [];
        var from = SlotGenerator.ToUtc(date.ToDateTime(hours.StartTime));
        var to = SlotGenerator.ToUtc(date.ToDateTime(hours.EndTime));
        if (from is null || to is null) return [];
        var occupied = await _dbContext.Bookings.AsNoTracking().Where(booking =>
            booking.ProviderId == service.ProviderId && booking.Status == BookingStatus.Confirmed
            && booking.StartTime < to.Value && booking.EndTime > from.Value)
            .Select(booking => new AvailableSlot(booking.StartTime, booking.EndTime))
            .ToListAsync(cancellationToken);
        return SlotGenerator.Generate(date, hours, service.DurationMinutes, occupied, now);
    }
}
