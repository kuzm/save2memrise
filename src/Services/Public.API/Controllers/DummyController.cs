using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Save2Memrise.Services.Public.API.Controllers
{
    [Route("dummy")]
    public class DummyController : Controller
    {
        [HttpGet()]
        public async Task<IActionResult> Get([FromQuery] int statusCode)
        {
            await Task.Delay(new System.Random().Next(1000));
            return StatusCode(statusCode: statusCode);
        }
    }
}
