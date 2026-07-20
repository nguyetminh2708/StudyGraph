using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StudyGraph.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController(IArangoDBClient client) : ControllerBase
    {
        /// <summary>GET /api/health — trả version ArangoDB, chứng minh API nối được DB (tuần 1).</summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var cursor = await client.Cursor.PostCursorAsync<string>(
                new PostCursorBody { Query = "RETURN VERSION()" });

            return Ok(new
            {
                Status = "healthy",
                ArangoVersion = cursor.Result.First()
            });
        }
    }
}
