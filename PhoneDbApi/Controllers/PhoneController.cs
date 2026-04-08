using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneDb.Api.Data;
using PhoneDb.Api.Models;
using PhoneDb.Api.Services;

namespace PhoneDb.Api.Controllers
{
    [ApiController]
    [Route("")]
    public class PhoneController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPhoneService _phoneService;
        private readonly ILockService _lockService;
        private readonly ILogger<PhoneController> _logger;

        public PhoneController(AppDbContext db, IPhoneService phoneService, ILockService lockService, ILogger<PhoneController> logger)
        {
            _db = db;
            _phoneService = phoneService;
            _lockService = lockService;
            _logger = logger;
        }

        [HttpPost("check_number")]
        public async Task<IActionResult> CheckNumber([FromBody] PhoneRequest request)
        {
            _logger.LogInformation("HTTP POST check_number called for: {Number}", request.Number);

            var result = _phoneService.ValidateAndNormalize(request.Number);

            if (!result.Success)
            {
                _logger.LogWarning("CheckNumber failed: validation error - {Error}", result.Error);
                return BadRequest(new { title = "Invalid Number", detail = result.Error });
            }

            var exists = await _db.PhoneNumbers.AnyAsync(p => p.Number == result.FormattedNumber);
            if (exists)
            {
                _logger.LogInformation("CheckNumber: Number {Number} already exists (quick check).", result.FormattedNumber);
                return Conflict(new { detail = "Number already exists in the database." });
            }

            _logger.LogDebug("Acquiring lock for number: {Number}", result.FormattedNumber);
            using (await _lockService.AcquireLockAsync(result.FormattedNumber!))
            {
                exists = await _db.PhoneNumbers.AnyAsync(p => p.Number == result.FormattedNumber);
                if (exists)
                {
                    _logger.LogInformation("CheckNumber: Number {Number} already exists (double-check).", result.FormattedNumber);
                    return Conflict(new { detail = "Number already exists in the database." });
                }

                var entry = new PhoneNumberEntry
                {
                    Number = result.FormattedNumber!,
                    CountryCode = result.CountryCode,
                    Region = result.Region,
                    CreatedAt = DateTime.UtcNow
                };

                _db.PhoneNumbers.Add(entry);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Successfully added new phone number to DB: {Number} (ID: {Id})", entry.Number, entry.Id);

                return Created($"/check_number", new
                {
                    entry.Id,
                    entry.Number,
                    entry.CreatedAt,
                    result.CountryCode,
                    result.Region
                });
            }
        }
    }
}
