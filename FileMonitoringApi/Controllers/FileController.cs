using Microsoft.AspNetCore.Mvc;
using FileMonitoring.Api.Services;

namespace FileMonitoring.Api.Controllers
{
    [ApiController]
    [Route("")]
    public class FileController : ControllerBase
    {
        private readonly IFileMonitorService _service;

        public FileController(IFileMonitorService service)
        {
            _service = service;
        }

        [HttpGet("count")]
        public IActionResult GetCount()
        {
            var count = _service.CountRecords();
            return Ok(new { Count = count });
        }

        [HttpPost("export")]
        public IActionResult Export()
        {
            var archived = _service.ExportAndArchive();
            if (archived == null)
            {
                return NotFound("The tracked CSV file was not found.");
            }

            return File(
                archived.Value.Content,
                "text/csv",
                archived.Value.Filename
            );
        }
    }
}
