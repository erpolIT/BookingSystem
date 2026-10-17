using BookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Data;

public static class DemoData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1648312457);");
        foreach (var (name, duration, price) in new[] { ("Adam", 30, 60m), ("Marta", 45, 80m) })
        {
            var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Name == name);
            if (provider is null)
            {
                provider = new Provider { Name = name };
                db.Providers.Add(provider);
                await db.SaveChangesAsync();
            }
            if (!await db.Services.AnyAsync(service => service.ProviderId == provider.Id && service.Name == "Haircut"))
                db.Services.Add(new Service { ProviderId = provider.Id, Name = "Haircut", DurationMinutes = duration, Price = price });
            for (var day = 1; day <= 5; day++)
            {
                var weekday = (DayOfWeek)day;
                if (!await db.WorkingHours.AnyAsync(hours => hours.ProviderId == provider.Id && hours.DayOfWeek == weekday))
                    db.WorkingHours.Add(new WorkingHours { ProviderId = provider.Id, DayOfWeek = weekday,
                        StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) });
            }
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        Console.WriteLine("Demo providers, services and weekday schedules are ready. Existing records were preserved.");
    }
}
