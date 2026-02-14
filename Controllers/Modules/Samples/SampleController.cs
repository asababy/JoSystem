using System;
using Microsoft.AspNetCore.Mvc;

namespace JoSystem.Controllers.Modules.Samples
{
    [ApiController]
    [Route("api/sample")]
    public class SampleController : ControllerBase
    {
        [HttpGet("hello")]
        public IActionResult Hello()
        {
            return Ok(new
            {
                success = true,
                module = "Sample",
                message = "Hello from business module",
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
}
