using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Controllers
{
    [ApiController]
    [Route("api/lessons")]
    public class LessonsController(EnrollmentRepository enrollments) : ControllerBase
    {
        /// <summary>POST /api/lessons/{key}/complete — hoàn thành bài + cập nhật Progress.</summary>
        [HttpPost("{key}/complete")]
        public async Task<IActionResult> Complete(string key)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            var progress = await enrollments.CompleteLessonAsync(user.Key, key);
            if (progress is null)
                return Conflict(new { Error = "Bạn chưa ghi danh khóa chứa bài học này" });

            return Ok(new { LessonKey = key, CourseProgress = progress });
        }
    }
}
