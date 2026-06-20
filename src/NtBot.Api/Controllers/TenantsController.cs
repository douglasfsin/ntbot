using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;
using NtBot.Domain.Entities;

namespace NtBot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class TenantsController : ControllerBase
    {
        private readonly NtBotDbContext _context;
        private readonly ILogger<TenantsController> _logger;

        public TenantsController(NtBotDbContext context, ILogger<TenantsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<Tenant>>> GetAll()
        {
            return await _context.Tenants
                .Include(t => t.Users)
                .Include(t => t.AssetConfigurations)
                .Where(t => t.IsActive)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tenant>> GetById(Guid id)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Users)
                .Include(t => t.AssetConfigurations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
                return NotFound();

            return tenant;
        }

        [HttpPost]
        public async Task<ActionResult<Tenant>> Create(Tenant tenant)
        {
            tenant.Id = Guid.NewGuid();
            tenant.CreatedAt = DateTime.UtcNow;
            tenant.IsActive = true;

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tenant created: {TenantId} - {TenantName}", tenant.Id, tenant.Name);

            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Tenant tenant)
        {
            if (id != tenant.Id)
                return BadRequest();

            tenant.UpdatedAt = DateTime.UtcNow;
            _context.Entry(tenant).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TenantExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
                return NotFound();

            tenant.IsActive = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tenant deactivated: {TenantId}", id);

            return NoContent();
        }

        private async Task<bool> TenantExists(Guid id)
        {
            return await _context.Tenants.AnyAsync(e => e.Id == id);
        }
    }
}
