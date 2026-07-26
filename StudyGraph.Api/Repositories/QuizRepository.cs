using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories;

public class QuizRepository(IArangoDBClient client)
{
    private const string GetByKeyAql = """
        RETURN DOCUMENT("quizzes", @key)
        """;

    private const string GetByLessonAql = """
        FOR q IN quizzes
          FILTER q.LessonKey == @lessonKey
          LIMIT 1
          RETURN q
        """;

    public async Task<Quiz?> GetAsync(string key)
    {
        var cursor = await client.Cursor.PostCursorAsync<Quiz?>(
            new PostCursorBody
            {
                Query = GetByKeyAql,
                BindVars = new Dictionary<string, object> { ["key"] = key }
            });
        return cursor.Result.FirstOrDefault();
    }

    public async Task<Quiz?> GetByLessonAsync(string lessonKey)
    {
        var cursor = await client.Cursor.PostCursorAsync<Quiz?>(
            new PostCursorBody
            {
                Query = GetByLessonAql,
                BindVars = new Dictionary<string, object> { ["lessonKey"] = lessonKey }
            });
        return cursor.Result.FirstOrDefault();
    }
}
