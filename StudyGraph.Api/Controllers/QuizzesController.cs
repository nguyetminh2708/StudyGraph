using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Models;
using StudyGraph.Api.Services;

namespace StudyGraph.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizzesController(
        QuizService quizService,
        StudyGraph.Api.Repositories.EnrollmentRepository enrollments) : ControllerBase
    {
        /// <summary>GET /api/quizzes/{key} — lấy đề (giấu AnswerIndex).</summary>
        [HttpGet("{key}")]
        public async Task<ActionResult<QuizView>> Get(string key)
        {
            var view = await quizService.GetViewAsync(key);
            return view is null ? NotFound() : Ok(view);
        }

        /// <summary>POST /api/quizzes/{key}/submit — nộp bài chấm điểm, lưu Score vào edge completed.</summary>
        [HttpPost("{key}/submit")]
        public async Task<ActionResult<QuizResult>> Submit(string key, [FromBody] QuizSubmission submission)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            var view = await quizService.GetViewAsync(key);
            if (view is null) return NotFound();

            // Chưa ghi danh thì chặn ngay — nếu để lọt, SubmitAsync vẫn chấm điểm và trả Passed
            // nhưng CompleteLessonAsync bỏ qua (không ghi edge completed) → UI báo thành công ảo
            if (!await enrollments.IsEnrolledForLessonAsync(user.Key, view.LessonKey))
                return Conflict(new { Error = "Bạn chưa ghi danh khóa chứa bài học này" });

            // Không cho nộp quiz của bài chưa mở (bài trước chưa hoàn thành)
            if (!await enrollments.PreviousLessonCompletedAsync(user.Key, view.LessonKey))
                return Conflict(new { Error = "Bạn cần hoàn thành bài học trước đó trước khi làm quiz này" });

            var result = await quizService.SubmitAsync(user.Key, key, submission);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
