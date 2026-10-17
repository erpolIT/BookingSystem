using BookingSystem.Models;

namespace BookingSystem.Services;

public static class SlotGenerator
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateOnly GetLocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Warsaw).DateTime);

    public static DateTimeOffset? ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Warsaw.IsInvalidTime(local) || Warsaw.IsAmbiguousTime(local)) return null;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Warsaw));
    }

    public static List<AvailableSlot> Generate(DateOnly date, WorkingHours hours,
        int durationMinutes, IReadOnlyCollection<AvailableSlot> occupied, DateTimeOffset now)
    {
        if (durationMinutes < 1 || durationMinutes > 480)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        var result = new List<AvailableSlot>();
        var today = GetLocalDate(now);
        if (date < today || date > today.AddYears(1) || hours.DayOfWeek != date.DayOfWeek) return result;
        var opening = date.ToDateTime(hours.StartTime);
        var closing = ToUtc(date.ToDateTime(hours.EndTime));
        if (hours.StartTime >= hours.EndTime || ToUtc(opening) is null || closing is null) return result;

        for (var localStart = opening; localStart < date.ToDateTime(hours.EndTime); localStart = localStart.AddMinutes(15))
        {
            var start = ToUtc(localStart);
            if (start is null || start.Value <= now) continue;
            var end = start.Value.AddMinutes(durationMinutes);
            if (end > closing.Value) continue;
            if (occupied.Any(booking => BookingTimeRules.Overlaps(start.Value, end, booking.StartTime, booking.EndTime))) continue;
            result.Add(new AvailableSlot(start.Value, end));
        }
        return result;
    }
}
