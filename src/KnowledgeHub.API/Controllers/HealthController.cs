using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.API.Controllers;

/// <summary>
/// Provides a health check endpoint for monitoring and container orchestration.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the current health status of the API.
    /// </summary>
    /// <returns>Health status with a UTC timestamp.</returns>
    /// <response code="200">The API is healthy and accepting requests.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
}
