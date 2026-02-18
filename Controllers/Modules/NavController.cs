using Microsoft.AspNetCore.Mvc;

namespace JoSystem.Controllers.Modules
{
    [ApiController]
    [Route("api/modules")]
    public class NavController : ControllerBase
    {
        [HttpGet("nav")]
        public IActionResult GetNav()
        {
            var items = new[]
            {
                new
                {
                    id = "quality-report",
                    name = "质量报告查询",
                    description = "从 Oracle WMS 库中按时间范围查看质量检测结果",
                    url = "/modules/quality/quality-report.html",
                    icon = "📊",
                    category = "QMSystem"
                }
            };

            return Ok(new
            {
                success = true,
                items
            });
        }
    }
}

