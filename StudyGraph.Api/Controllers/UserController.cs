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
    RecommendationService recommendationService,
    UserRepository users) : ControllerBase
    {
        /// <summary>
        /// POST /api/user/login — đăng nhập tối giản cho student:
        /// nhập Email, trả về UserKey để client gắn vào header X-User-Key
        /// cho các request sau. Không password/JWT — ghi vào "hướng phát triển".
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { Error = "Email không được để trống" });

            var user = await users.GetByEmailAsync(request.Email.Trim());
            if (user is null)
                return Unauthorized(new { Error = "Email không tồn tại trong hệ thống" });

            if (user.Role != "student")
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { Error = "Endpoint này chỉ dành cho student" });

            return Ok(new LoginResponse
            {
                UserKey = user.Key,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role
            });
        }

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
