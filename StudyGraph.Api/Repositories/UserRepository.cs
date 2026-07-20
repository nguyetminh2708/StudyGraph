using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories
{
    public class UserRepository(IArangoDBClient client)
    {
        private const string GetByKeyAql = """
        RETURN DOCUMENT("users", @key)
        """;

        public async Task<User?> GetAsync(string key)
        {
            var cursor = await client.Cursor.PostCursorAsync<User?>(
                new PostCursorBody
                {
                    Query = GetByKeyAql,
                    BindVars = new Dictionary<string, object> { ["key"] = key }
                });
            return cursor.Result.FirstOrDefault();
        }
    }
}
