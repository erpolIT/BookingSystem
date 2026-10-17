using Microsoft.AspNetCore.Authorization;
using BookingSystem.Contracts;
using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Controllers;

[ApiController]
[Route("api/providers/{providerId:int}/working-hours")]
public class WorkingHoursController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public WorkingHoursController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkingHours>>> GetWorkingHours(int providerId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Providers.AnyAsync(provider => provider.Id == providerId, cancellationToken))
            return NotFound();

        return Ok(await _dbContext.WorkingHours.AsNoTracking()
            .Where(hours => hours.ProviderId == providerId)
            .OrderBy(hours => hours.DayOfWeek).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{day:int}")]
    public async Task<IActionResult> SetWorkingHours(int providerId, int day,
        SetWorkingHoursRequest request, CancellationToken cancellationToken)
    {
        if (day < 0 || day > 6)
        {
            ModelState.AddModelError(nameof(day), "Day must be between 0 (Sunday) and 6 (Saturday).");
            return ValidationProblem(ModelState);
        }

        if (!await _dbContext.Providers.AnyAsync(provider => provider.Id == providerId, cancellationToken))
            return NotFound();

        // Atomic upsert prevents duplicate days even when requests arrive concurrently.
        var weekday = day;
        var start = request.StartTime!.Value;
        var end = request.EndTime!.Value;
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""WorkingHours"" (""ProviderId"", ""DayOfWeek"", ""StartTime"", ""EndTime"")
            VALUES ({providerId}, {weekday}, {start}, {end})
            ON CONFLICT (""ProviderId"", ""DayOfWeek"")
            DO UPDATE SET ""StartTime"" = EXCLUDED.""StartTime"", ""EndTime"" = EXCLUDED.""EndTime"";",
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = "Administrator")]
    [HttpDelete("{day:int}")]
    public async Task<IActionResult> RemoveWorkingHours(int providerId, int day, CancellationToken cancellationToken)
    {
        if (day < 0 || day > 6) return BadRequest("Day must be between 0 and 6.");
        if (!await _dbContext.Providers.AnyAsync(provider => provider.Id == providerId, cancellationToken))
            return NotFound();

        await _dbContext.WorkingHours
            .Where(hours => hours.ProviderId == providerId && hours.DayOfWeek == (DayOfWeek)day)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }
}
