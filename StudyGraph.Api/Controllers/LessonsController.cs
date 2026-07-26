using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Controllers
{
    [ApiController]
    [Route("api/lessons")]
    public class LessonsController(
        EnrollmentRepository enrollments,
        CourseRepository courses,
        QuizRepository quizzes) : ControllerBase
    {
        /// <summary>GET /api/lessons/{key} — nội dung bài + key quiz (nếu có) + trạng thái hoàn thành.</summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key)
        {
            var lesson = await courses.GetLessonAsync(key);
            if (lesson is null) return NotFound();

            var quiz = await quizzes.GetByLessonAsync(key);
            var user = HttpContext.CurrentUser();
            var completedEdge = user is null ? null : await enrollments.GetCompletedEdgeAsync(user.Key, key);
            return Ok(new
            {
                Lesson = lesson,
                QuizKey = quiz?.Key,
                Completed = completedEdge is not null,
                completedEdge?.Score
            });
        }

        /// <summary>POST /api/lessons/{key}/complete — hoàn thành bài + cập nhật Progress.</summary>
        [HttpPost("{key}/complete")]
        public async Task<IActionResult> Complete(string key)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            // Học tuần tự: bài trước phải hoàn thành rồi mới được học bài này
            if (!await enrollments.PreviousLessonCompletedAsync(user.Key, key))
                return Conflict(new { Error = "Bạn cần hoàn thành bài học trước đó trước khi qua bài này" });

            // Bài có quiz thì không hoàn thành tay được — phải nộp quiz đạt >= 80%
            var quiz = await quizzes.GetByLessonAsync(key);
            if (quiz is not null)
                return Conflict(new { Error = $"Bài này có quiz — nộp quiz đạt tối thiểu {Services.QuizService.PassScore}% để hoàn thành" });

            var progress = await enrollments.CompleteLessonAsync(user.Key, key);
            if (progress is null)
                return Conflict(new { Error = "Bạn chưa ghi danh khóa chứa bài học này" });

            return Ok(new { LessonKey = key, CourseProgress = progress });
        }
    }
}
