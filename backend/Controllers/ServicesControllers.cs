using Microsoft.AspNetCore.Authorization;
using BookingSystem.Contracts;
using BookingSystem.Data;
using BookingSystem.Models;
using BookingSystem.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public ServicesController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<Service>>> GetServices(CancellationToken cancellationToken)
    {
        var services = await _dbContext.Services
            .AsNoTracking()
            .OrderBy(service => service.Id)
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Service>> GetServiceById(int id, CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

        if (service is null)
        {
            return NotFound();
        }

        return Ok(service);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<ActionResult<Service>> CreateService(
        CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Providers.AnyAsync(provider => provider.Id == request.ProviderId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.ProviderId), "Provider does not exist.");
            return ValidationProblem(ModelState);
        }

        var service = new Service
        {
            ProviderId = request.ProviderId,
            Name = request.Name.Trim(),
            DurationMinutes = request.DurationMinutes,
            Price = request.Price
        };

        if (service.Name.Length < 2)
        {
            ModelState.AddModelError(nameof(request.Name), "Name must contain at least two non-whitespace characters.");
            return ValidationProblem(ModelState);
        }

        _dbContext.Services.Add(service);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetServiceById), new { id = service.Id }, service);
    }
    [HttpGet("{id:int}/available-slots")]
    public async Task<ActionResult<List<AvailableSlot>>> GetAvailableSlots(int id,
        [FromQuery, Required] DateOnly? date,
        [FromServices] AvailabilityService availability,
        CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services.AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == id, cancellationToken);
        if (service is null) return NotFound();
        return Ok(await availability.GetSlots(service, date!.Value, cancellationToken));
    }

}
