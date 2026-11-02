using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BookingSystem.Services;

public sealed record CreateBookingCommand(string UserId, int ServiceId, DateTimeOffset StartTime,
    string CustomerName, string CustomerEmail, Guid IdempotencyKey);

public class BookingService
{
    private readonly BookingDbContext _dbContext;
    private readonly AvailabilityService _availability;

    public BookingService(BookingDbContext dbContext, AvailabilityService availability)
    {
        _dbContext = dbContext;
        _availability = availability;
    }

    public async Task<CreateBookingResult> CreateAsync(CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var name = command.CustomerName.Trim();
        if (name.Length < 2)
        {
            return new CreateBookingResult.InvalidCustomerName();
        }
        var start = command.StartTime.ToUniversalTime();
        var email = command.CustomerEmail.Trim();
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { command.ServiceId, StartTime = start, CustomerName = name, CustomerEmail = email }))));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize requests with the same key across all API instances.
        // The lock is released automatically when the transaction ends.
        var lockKey = BitConverter.ToInt64(SHA256.HashData(command.IdempotencyKey.ToByteArray()), 0);
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey});", cancellationToken);

        var existing = await _dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.IdempotencyKey == command.IdempotencyKey && booking.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                return new CreateBookingResult.IdempotencyKeyReused();
            await transaction.CommitAsync(cancellationToken);
            return new CreateBookingResult.AlreadyProcessed(existing);
        }

        var service = await _dbContext.Services.AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == command.ServiceId, cancellationToken);
        if (service is null) return new CreateBookingResult.ServiceNotFound();

        // Exclusion checks on simultaneous inserts may deadlock in PostgreSQL.
        // Serialize each provider's booking writes before reading availability.
        // The two-int namespace is distinct from the bigint idempotency locks.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(72100, {service.ProviderId});", cancellationToken);

        var slots = await _availability.GetSlots(service, AvailabilityService.GetLocalDate(start), cancellationToken);
        if (!slots.Any(slot => slot.StartTime == start))
            return new CreateBookingResult.SlotUnavailable();

        var booking = new Booking
        {
            UserId = userId,
            IdempotencyKey = command.IdempotencyKey,
            RequestHash = requestHash,
            ServiceId = service.Id,
            ProviderId = service.ProviderId,
            StartTime = start,
            EndTime = start.AddMinutes(service.DurationMinutes),
            PriceAtBooking = service.Price,
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
            return new CreateBookingResult.SlotJustBooked();
        }
        await transaction.CommitAsync(cancellationToken);
        return new CreateBookingResult.Created(booking);
    }
}
