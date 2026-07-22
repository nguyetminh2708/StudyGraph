using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Middleware
{
    public class AuthenticationMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-User-Key";
        public const string ItemKey = "CurrentUser";

        public async Task InvokeAsync(HttpContext context, UserRepository users)
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var userKey)
                && !string.IsNullOrWhiteSpace(userKey))
            {
                var user = await users.GetAsync(userKey.ToString());
                if (user is not null)
                    context.Items[ItemKey] = user;
            }

            await next(context);
        }
    }
    public static class HttpContextUserExtensions
    {
        /// <summary>User đã xác thực qua X-User-Key, hoặc null nếu thiếu/sai header.</summary>
        public static User? CurrentUser(this HttpContext context)
            => context.Items[AuthenticationMiddleware.ItemKey] as User;
    }

}
