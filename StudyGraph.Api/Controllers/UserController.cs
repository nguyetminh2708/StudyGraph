using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;
using StudyGraph.Api.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StudyGraph.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(
    EnrollmentRepository enrollments,
    RecommendationService recommendationService) : ControllerBase
    {
        /// <summary>GET /api/user/progress — tiến độ các khóa đã ghi danh.</summary>
        [HttpGet("progress")]
        public async Task<ActionResult<List<ProgressItem>>> Progress()
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            return Ok(await enrollments.GetMyProgressAsync(user.Id));
        }

        /// <summary>GET /api/user/recommendations — gợi ý trộn Q1 + Q2, có lý do từng gợi ý.</summary>
        [HttpGet("recommendations")]
        public async Task<ActionResult<List<RecommendationItem>>> Recommendations()
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            return Ok(await recommendationService.GetForUserAsync(user.Key));
        }
    }
}
