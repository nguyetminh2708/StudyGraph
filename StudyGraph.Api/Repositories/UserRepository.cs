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

        // Email có unique index (mục 3 schema) → tối đa 1 kết quả, LOWER để không phân biệt hoa thường
        private const string GetByEmailAql = """
        FOR u IN users
          FILTER LOWER(u.Email) == LOWER(@email)
          LIMIT 1
          RETURN u
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

        public async Task<User?> GetByEmailAsync(string email)
        {
            var cursor = await client.Cursor.PostCursorAsync<User?>(
                new PostCursorBody
                {
                    Query = GetByEmailAql,
                    BindVars = new Dictionary<string, object> { ["email"] = email }
                });
            return cursor.Result.FirstOrDefault();
        }
    }
}
