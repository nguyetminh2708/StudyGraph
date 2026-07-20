using Microsoft.AspNetCore.Mvc;
using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StudyGraph.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController(CourseRepository courses) : ControllerBase
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

        // POST api/<CoursesController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<CoursesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<CoursesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
