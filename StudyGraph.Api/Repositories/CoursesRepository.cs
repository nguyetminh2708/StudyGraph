using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories
{
    public class CourseRepository(IArangoDBClient client)
    {
        private const string ListAql = """
        LET filtered = (
          FOR c IN courses
            FILTER @category == null OR c.Category == @category
            FILTER @level == null OR c.Level == @level
            RETURN c
        )
        LET page = (
          FOR c IN filtered
            SORT c.Level, c.Title
            LIMIT @offset, @count
            RETURN c
        )
        RETURN { Items: page, Total: LENGTH(filtered) }
        """;

        private const string GetByKeyAql = """
        RETURN DOCUMENT("courses", @key)
        """;

        private const string LessonsByCourseAql = """
        FOR l IN lessons
          FILTER l.CourseKey == @courseKey
          SORT l.Order
          RETURN l
        """;

        // Q3 — Learning path: cần học gì trước khi vào 1 khóa nâng cao (nguyên văn mục 5)
        // Toàn bộ chuỗi điều kiện (đệ quy tới 5 tầng), kèm độ sâu để vẽ lộ trình
        private const string LearningPathAql = """        
        FOR v, e, p IN 1..5 INBOUND @courseId prerequisite_of
          OPTIONS { uniqueVertices: "global" }
          RETURN DISTINCT { Course: v, Depth: LENGTH(p.edges) }
        """;

        private const string UpsertAql = """
            UPSERT { _key: @key }
            INSERT MERGE({ _key: @key }, @doc)
            UPDATE @doc
            IN courses
            RETURN NEW
            """;

        private class ListPage
        {
            public List<Course> Items { get; set; } = new();
            public long Total { get; set; }
        }

        public async Task<PagedResult<Course>> ListAsync(
            string? category, int? level, int page, int pageSize)
        {
            var cursor = await client.Cursor.PostCursorAsync<ListPage>(
                new PostCursorBody
                {
                    Query = ListAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["category"] = category!,
                        ["level"] = level!,
                        ["offset"] = (page - 1) * pageSize,
                        ["count"] = pageSize
                    }
                });
            var result = cursor.Result.First();
            return new PagedResult<Course>
            {
                Items = result.Items,
                Page = page,
                PageSize = pageSize,
                Total = result.Total
            };
        }

        public async Task<Course?> GetAsync(string key)
        {
            var cursor = await client.Cursor.PostCursorAsync<Course?>(
                new PostCursorBody
                {
                    Query = GetByKeyAql,
                    BindVars = new Dictionary<string, object> { ["key"] = key }
                });
            return cursor.Result.FirstOrDefault();
        }

        public async Task<List<Lesson>> GetLessonsAsync(string courseKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<Lesson>(
                new PostCursorBody
                {
                    Query = LessonsByCourseAql,
                    BindVars = new Dictionary<string, object> { ["courseKey"] = courseKey }
                });
            return cursor.Result.ToList();
        }

        public async Task<List<LearningPathStep>> GetLearningPathAsync(string courseKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<LearningPathStep>(
                new PostCursorBody
                {
                    Query = LearningPathAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["courseId"] = $"courses/{courseKey}"
                    }
                });
            return cursor.Result.ToList();
        }

        public async Task<Course> UpsertAsync(string key, CourseUpsertRequest req)
        {
            var cursor = await client.Cursor.PostCursorAsync<Course>(
                new PostCursorBody
                {
                    Query = UpsertAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["key"] = key,
                        ["doc"] = new Dictionary<string, object>
                        {
                            ["Title"] = req.Title,
                            ["Category"] = req.Category,
                            ["Level"] = req.Level,
                            ["Description"] = req.Description,
                            ["Tags"] = req.Tags
                        }
                    }
                });
            return cursor.Result.First();
        }
    }
}
