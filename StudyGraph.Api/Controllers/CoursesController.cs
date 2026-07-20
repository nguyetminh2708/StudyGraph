using ArangoDBNetStandard;
using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Models;
using StudyGraph.Api.NewFolder;
using StudyGraph.Api.Repositories;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StudyGraph.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController(CourseRepository courses, EnrollmentRepository enrollments) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<PagedResult<Course>>> List(
        [FromQuery] string? category,
        [FromQuery] int? level,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 1 or > 100) pageSize = 10;
            return Ok(await courses.ListAsync(category, level, page, pageSize));
        }

        /// <summary>GET /api/courses/{key} — chi tiết khóa + lessons (sort Order).</summary>
        [HttpGet("{key}")]
        public async Task<ActionResult<CourseDetailDto>> Get(string key)
        {
            var course = await courses.GetAsync(key);
            if (course is null) return NotFound();

            return Ok(new CourseDetailDto
            {
                Course = course,
                Lessons = await courses.GetLessonsAsync(key)
            });
        }

        /// <summary>POST /api/courses — tạo khóa (admin).</summary>
        [HttpPost]
        public async Task<ActionResult<Course>> Create([FromBody] CourseUpsertRequest req)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });
            if (user.Role != "admin") return Forbid();

            var key = string.IsNullOrWhiteSpace(req.Key)
                ? $"c-{Guid.NewGuid():N}"[..12]
                : req.Key;
            var created = await courses.UpsertAsync(key, req);
            return CreatedAtAction(nameof(Get), new { key = created.Key }, created);
        }

        /// <summary>PUT /api/courses/{key} — sửa khóa (admin).</summary>
        [HttpPut("{key}")]
        public async Task<ActionResult<Course>> Update(string key, [FromBody] CourseUpsertRequest req)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });
            if (user.Role != "admin") return Forbid();

            return Ok(await courses.UpsertAsync(key, req));
        }

        /// <summary>POST /api/courses/{key}/enroll — ghi danh (edge enrolled_in, chặn trùng).</summary>
        [HttpPost("{key}/enroll")]
        public async Task<IActionResult> Enroll(string key)
        {
            var user = HttpContext.CurrentUser();
            if (user is null) return Unauthorized(new { Error = "Thiếu hoặc sai header X-User-Key" });

            var course = await courses.GetAsync(key);
            if (course is null) return NotFound();

            try
            {
                var edge = await enrollments.EnrollAsync(user.Key, key);
                return StatusCode(StatusCodes.Status201Created, edge);
            }
            catch (ApiErrorException ex)
                when (ex.ApiError?.ErrorNum == EnrollmentRepository.ErrUniqueConstraintViolated)
            {
                // unique index [_from,_to] trên enrolled_in (mục 3) bắn lỗi 1210 khi ghi danh trùng
                return Conflict(new { Error = "Bạn đã ghi danh khóa này rồi" });
            }
        }

        /// <summary>GET /api/courses/{key}/learning-path — chuỗi khóa cần học trước (Q3).</summary>
        [HttpGet("{key}/learning-path")]
        public async Task<ActionResult<List<LearningPathStep>>> LearningPath(string key)
        {
            var course = await courses.GetAsync(key);
            if (course is null) return NotFound();

            var steps = await courses.GetLearningPathAsync(key);
            // Depth lớn = phải học trước tiên → sort giảm dần thành thứ tự học
            return Ok(steps.OrderByDescending(s => s.Depth).ToList());
        }
    }
}
