using Microsoft.AspNetCore.Authorization;
using BookingSystem.Contracts;
using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public ProvidersController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<Provider>>> GetProviders(CancellationToken cancellationToken)
    {
        return Ok(await _dbContext.Providers.AsNoTracking()
            .OrderBy(provider => provider.Id).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Provider>> GetProviderById(int id, CancellationToken cancellationToken)
    {
        var provider = await _dbContext.Providers.AsNoTracking()
            .FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);
        if (provider is null) return NotFound();
        return Ok(provider);
    }

    [HttpGet("{id:int}/services")]
    public async Task<ActionResult<List<Service>>> GetProviderServices(int id, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Providers.AnyAsync(provider => provider.Id == id, cancellationToken))
            return NotFound();

        return Ok(await _dbContext.Services.AsNoTracking()
            .Where(service => service.ProviderId == id)
            .OrderBy(service => service.Id).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<ActionResult<Provider>> CreateProvider(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length < 2)
        {
            ModelState.AddModelError(nameof(request.Name), "Name must contain at least two non-whitespace characters.");
            return ValidationProblem(ModelState);
        }

        var provider = new Provider { Name = name };
        _dbContext.Providers.Add(provider);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetProviderById), new { id = provider.Id }, provider);
    }
}
