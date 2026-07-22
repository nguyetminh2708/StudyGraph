using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Middleware;
using StudyGraph.Api.Models;
using StudyGraph.Api.Services;

namespace StudyGraph.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizzesController(QuizService quizService) : ControllerBase
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

            var result = await quizService.SubmitAsync(user.Key, key, submission);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
