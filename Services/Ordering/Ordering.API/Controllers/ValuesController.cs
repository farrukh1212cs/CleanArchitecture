using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ordering.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {

        [HttpGet("test")] // Specify the HTTP method here
        public IActionResult Test()
        {

            return Ok("Hello");
        }
    }
}
