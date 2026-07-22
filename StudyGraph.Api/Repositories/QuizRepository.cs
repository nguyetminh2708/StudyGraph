using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories;

public class QuizRepository(IArangoDBClient client)
{
    private const string GetByKeyAql = """
        RETURN DOCUMENT("quizzes", @key)
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
}
