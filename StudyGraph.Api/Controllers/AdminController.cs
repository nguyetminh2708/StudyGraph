using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController(CourseRepository courses) : ControllerBase
    {
        /// <summary>GET /api/admin/stats — tổng quan hệ thống + thống kê từng khóa (admin).</summary>
        [HttpGet("stats")]
        public async Task<ActionResult<AdminStatsDto>> Stats()
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });
            if (user.Role != "admin")
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { Error = "Chức năng này chỉ dành cho admin" });

            return Ok(await courses.GetAdminStatsAsync());
        }
    }
}
