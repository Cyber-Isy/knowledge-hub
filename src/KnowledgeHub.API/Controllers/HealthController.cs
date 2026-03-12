using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
}
