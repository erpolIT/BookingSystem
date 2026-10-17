namespace BookingSystem.Services;

public static class BookingTimeRules
{
    public static bool Overlaps(DateTimeOffset firstStart, DateTimeOffset firstEnd,
        DateTimeOffset secondStart, DateTimeOffset secondEnd)
    {
        if (firstStart >= firstEnd) throw new ArgumentException("The first interval must have positive duration.");
        if (secondStart >= secondEnd) throw new ArgumentException("The second interval must have positive duration.");
        // Half-open intervals allow one visit to start exactly when another ends.
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
