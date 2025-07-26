using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            _logger.LogInformation("Health check requested");
            return Ok(new 
            { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                version = "1.0.0"
            });
        }

        [HttpGet("config")]
        public IActionResult Config()
        {
            var config = new
            {
                hasRedis = !string.IsNullOrEmpty(Configuration.GetConnectionString("Redis")),
                hasSqlServer = !string.IsNullOrEmpty(Configuration.GetConnectionString("SqlServer")),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            };
            
            return Ok(config);
        }

        private IConfiguration Configuration => HttpContext.RequestServices.GetRequiredService<IConfiguration>();
    }
}