using System;
using Microsoft.AspNetCore.Mvc;
using JoSystem.Services;

namespace JoSystem.Controllers
{
    [ApiController]
    [Route("api")]
    public class LogController : ControllerBase
    {
        [HttpGet("logs")]
        public IActionResult GetLogs(
            [FromQuery] string keyword,
            [FromQuery] string level,
            [FromQuery] string source,
            [FromQuery] DateTime? startTime,
            [FromQuery] DateTime? endTime,
            [FromQuery] int? pageIndex,
            [FromQuery] int? pageSize)
        {
            int page = pageIndex.HasValue && pageIndex > 0 ? pageIndex.Value : 1;
            int size = pageSize.HasValue && pageSize > 0 ? pageSize.Value : ConfigService.Current.LogPageSize;

            var result = LogService.GetLogs(keyword, level, source, startTime, endTime, page, size);
            return Ok(result);
        }
    }
}
