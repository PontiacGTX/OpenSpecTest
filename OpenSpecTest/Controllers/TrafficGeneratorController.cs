using Microsoft.AspNetCore.Mvc;
using OpenSpec.Application.Services;
using OpenSpec.Domain.Models;

namespace OpenSpec.API.Controllers
{
    [ApiController]
    [Route("api/test/traffic")]
    public class TrafficGeneratorController : ControllerBase
    {
        private readonly AuditTrafficGenerator _generator;

        public TrafficGeneratorController(AuditTrafficGenerator generator)
        {
            _generator = generator;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunScenario([FromQuery] TrafficScenario scenario, [FromQuery] int iterations = 5)
        {
            // Ejecuta el harness en segundo plano
            _ = Task.Run(() => _generator.RunScenarioAsync(scenario, iterations));

            return Accepted(new
            {
                Status = "Traffic generation started",
                Scenario = scenario.ToString(),
                Iterations = iterations
            });
        }
    }
}
