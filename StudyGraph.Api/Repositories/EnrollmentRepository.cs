using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories
{
    public class EnrollmentRepository(IArangoDBClient client)
    {
        /// <summary>Lỗi ArangoDB khi vi phạm unique index [_from,_to] — dùng chặn ghi danh trùng.</summary>
        public const int ErrUniqueConstraintViolated = 1210;

        private const string EnrollAql = """
        INSERT {
          _from: @userId,
          _to: @courseId,
          EnrolledAt: @now,
          Progress: 0
        } IN enrolled_in
        RETURN NEW
        """;

        private const string MyCourseIdsAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId
          RETURN e._to
        """;

        // @myCompletedCourseIds trong Q2 = các khóa có Progress 100 (tính từ enrolled_in) — mục 5
        private const string MyCompletedCourseIdsAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e.Progress == 100
          RETURN e._to
        """;

        private const string UpsertCompletedAql = """
        UPSERT { _from: @userId, _to: @lessonId }
        INSERT { _from: @userId, _to: @lessonId, CompletedAt: @now, Score: @score }
        UPDATE @score == null ? { CompletedAt: @now } : { CompletedAt: @now, Score: @score }
        IN completed
        RETURN NEW
        """;

        // Tính lại % hoàn thành rồi ghi vào edge enrolled_in.
        // Tách riêng khỏi UpsertCompletedAql vì AQL cấm đọc collection vừa sửa trong cùng 1 query.
        private const string RecomputeProgressAql = """
        LET lesson = DOCUMENT(@lessonId)
        LET courseId = CONCAT("courses/", lesson.CourseKey)
        LET total = LENGTH(FOR l IN lessons FILTER l.CourseKey == lesson.CourseKey RETURN 1)
        LET done = LENGTH(
          FOR c IN completed
            FILTER c._from == @userId
            LET ld = DOCUMENT(c._to)
            FILTER ld.CourseKey == lesson.CourseKey
            RETURN 1
        )
        LET progress = total == 0 ? 0 : ROUND(100 * done / total)
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e._to == courseId
          UPDATE e WITH { Progress: progress } IN enrolled_in
          RETURN NEW.Progress
        """;

        private const string MyProgressAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId
          LET c = DOCUMENT(e._to)
          SORT e.EnrolledAt DESC
          RETURN { Course: c, Progress: e.Progress, EnrolledAt: e.EnrolledAt }
        """;

        public async Task<EnrolledInEdge> EnrollAsync(string userKey, string courseKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<EnrolledInEdge>(
                new PostCursorBody
                {
                    Query = EnrollAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["courseId"] = $"courses/{courseKey}",
                        ["now"] = DateTime.UtcNow.ToString("o")
                    }
                });
            return cursor.Result.First();
        }

        public async Task<List<string>> GetMyCourseIdsAsync(string userId)
        {
            var cursor = await client.Cursor.PostCursorAsync<string>(
                new PostCursorBody
                {
                    Query = MyCourseIdsAql,
                    BindVars = new Dictionary<string, object> { ["userId"] = userId }
                });
            return cursor.Result.ToList();
        }

        public async Task<List<string>> GetMyCompletedCourseIdsAsync(string userId)
        {
            var cursor = await client.Cursor.PostCursorAsync<string>(
                new PostCursorBody
                {
                    Query = MyCompletedCourseIdsAql,
                    BindVars = new Dictionary<string, object> { ["userId"] = userId }
                });
            return cursor.Result.ToList();
        }

        /// <summary>
        /// Đánh dấu hoàn thành bài học (edge completed, idempotent nhờ UPSERT),
        /// kèm Score nếu là nộp quiz, rồi cập nhật lại Progress trên enrolled_in.
        /// Trả về Progress mới của khóa, hoặc null nếu user chưa ghi danh khóa chứa bài này.
        /// </summary>
        public async Task<int?> CompleteLessonAsync(string userKey, string lessonKey, int? score = null)
        {
            var userId = $"users/{userKey}";
            var lessonId = $"lessons/{lessonKey}";

            await client.Cursor.PostCursorAsync<CompletedEdge>(
                new PostCursorBody
                {
                    Query = UpsertCompletedAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = userId,
                        ["lessonId"] = lessonId,
                        ["now"] = DateTime.UtcNow.ToString("o"),
                        ["score"] = score!
                    }
                });

            var progressCursor = await client.Cursor.PostCursorAsync<int?>(
                new PostCursorBody
                {
                    Query = RecomputeProgressAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = userId,
                        ["lessonId"] = lessonId
                    }
                });
            return progressCursor.Result.FirstOrDefault();
        }

        public async Task<List<ProgressItem>> GetMyProgressAsync(string userId)
        {
            var cursor = await client.Cursor.PostCursorAsync<ProgressItem>(
                new PostCursorBody
                {
                    Query = MyProgressAql,
                    BindVars = new Dictionary<string, object> { ["userId"] = userId }
                });
            return cursor.Result.ToList();
        }
    }
}
