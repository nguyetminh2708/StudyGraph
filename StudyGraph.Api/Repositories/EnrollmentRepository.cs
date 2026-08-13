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

        // Chưa ghi danh thì KHÔNG được ghi edge completed — check trước khi upsert
        private const string IsEnrolledForLessonAql = """
        LET lesson = DOCUMENT(@lessonId)
        LET courseId = CONCAT("courses/", lesson.CourseKey)
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e._to == courseId
          RETURN 1
        """;

        // Tính lại Progress ngay khi ghi danh — phòng user đã có sẵn edge completed
        // (data cũ bị lệch, hoặc re-enroll) thì % hiển thị đúng ngay từ đầu
        private const string RecomputeProgressByCourseAql = """
        LET total = LENGTH(FOR l IN lessons FILTER l.CourseKey == @courseKey RETURN 1)
        LET done = LENGTH(
          FOR c IN completed
            FILTER c._from == @userId
            LET ld = DOCUMENT(c._to)
            FILTER ld.CourseKey == @courseKey
            RETURN 1
        )
        LET progress = total == 0 ? 0 : FLOOR(100 * done / total)
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e._to == CONCAT("courses/", @courseKey)
          UPDATE e WITH { Progress: progress } IN enrolled_in
          RETURN NEW
        """;

        private const string MyCourseIdsAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId
          RETURN e._to
        """;

        // @myCompletedCourseIds trong Q2 = các khóa có Progress 100 (tính từ enrolled_in)
        private const string MyCompletedCourseIdsAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e.Progress == 100
          RETURN e._to
        """;

        private const string UpsertCompletedAql = """
        UPSERT { _from: @userId, _to: @lessonId }
        INSERT { _from: @userId, _to: @lessonId, CompletedAt: @now, Score: @score }
        UPDATE (@score == null ? { CompletedAt: @now } : { CompletedAt: @now, Score: @score })
        IN completed
        RETURN NEW
        """;

        // Tính lại % hoàn thành rồi ghi vào edge enrolled_in.
        // Tách riêng khỏi UpsertCompletedAql vì AQL cấm đọc collection vừa sửa trong cùng 1 query.
        // Dùng FLOOR (không ROUND): tránh 199/200 làm tròn thành 100% → khóa bị coi là
        // hoàn thành (Progress == 100 dùng trong MyCompletedCourseIdsAql) khi chưa học hết.
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
        LET progress = total == 0 ? 0 : FLOOR(100 * done / total)
        FOR e IN enrolled_in
          FILTER e._from == @userId AND e._to == courseId
          UPDATE e WITH { Progress: progress } IN enrolled_in
          RETURN NEW.Progress
        """;

        // Học tuần tự: bài N chỉ mở khi bài liền trước (cùng khóa) đã có edge completed.
        // Edge completed của bài có quiz chỉ được tạo khi quiz đạt >= 80% (QuizService),
        // nên check này đồng thời bao luôn rule "quiz phải đạt 80% mới qua bài tiếp theo".
        // "Bài liền trước" = bài có Order LỚN NHẤT nhỏ hơn Order hiện tại (không dùng Order - 1
        // vì admin xóa bài giữa chừng có thể để lại lỗ trong dãy Order → check bị vô hiệu).
        private const string PreviousLessonCompletedAql = """
        LET lesson = DOCUMENT(@lessonId)
        LET prev = FIRST(
          FOR l IN lessons
            FILTER l.CourseKey == lesson.CourseKey AND l.Order < lesson.Order
            SORT l.Order DESC
            LIMIT 1
            RETURN l
        )
        RETURN prev == null
          ? true
          : LENGTH(
              FOR c IN completed
                FILTER c._from == @userId AND c._to == prev._id
                LIMIT 1
                RETURN 1
            ) > 0
        """;

    private const string MyProgressAql = """
        FOR e IN enrolled_in
          FILTER e._from == @userId
          LET c = DOCUMENT(e._to)
          SORT e.EnrolledAt DESC
          RETURN { Course: c, Progress: e.Progress, EnrolledAt: e.EnrolledAt }
        """;

        private const string CompletedEdgeAql = """
        FOR e IN completed
          FILTER e._from == @userId AND e._to == @lessonId
          RETURN e
        """;

        private const string CompletedLessonKeysAql = """
        FOR e IN completed
          FILTER e._from == @userId
          LET l = DOCUMENT(e._to)
          FILTER l.CourseKey == @courseKey
          RETURN l._key
        """;

        /// <summary>Bài liền trước (theo Order) đã hoàn thành chưa? Bài đầu tiên luôn true.</summary>
        public async Task<bool> PreviousLessonCompletedAsync(string userKey, string lessonKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<bool>(
                new PostCursorBody
                {
                    Query = PreviousLessonCompletedAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["lessonId"] = $"lessons/{lessonKey}"
                    }
                });
            return cursor.Result.FirstOrDefault();
        }

        /// <summary>User đã ghi danh khóa chứa bài học này chưa? Dùng chặn cả complete tay lẫn nộp quiz.</summary>
        public async Task<bool> IsEnrolledForLessonAsync(string userKey, string lessonKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<int>(
                new PostCursorBody
                {
                    Query = IsEnrolledForLessonAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["lessonId"] = $"lessons/{lessonKey}"
                    }
                });
            return cursor.Result.Any();
        }

        public async Task<EnrolledInEdge> EnrollAsync(string userKey, string courseKey)
        {
            await client.Cursor.PostCursorAsync<EnrolledInEdge>(
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

            var recomputed = await client.Cursor.PostCursorAsync<EnrolledInEdge>(
                new PostCursorBody
                {
                    Query = RecomputeProgressByCourseAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["courseKey"] = courseKey
                    }
                });
            return recomputed.Result.First();
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

            if (!await IsEnrolledForLessonAsync(userKey, lessonKey)) return null;

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

        /// <summary>Edge completed của user với 1 bài học — null nếu chưa hoàn thành.</summary>
        public async Task<CompletedEdge?> GetCompletedEdgeAsync(string userKey, string lessonKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<CompletedEdge?>(
                new PostCursorBody
                {
                    Query = CompletedEdgeAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["lessonId"] = $"lessons/{lessonKey}"
                    }
                });
            return cursor.Result.FirstOrDefault();
        }

        /// <summary>Key các bài học user đã hoàn thành trong 1 khóa — để UI đánh dấu ✓.</summary>
        public async Task<List<string>> GetCompletedLessonKeysAsync(string userKey, string courseKey)
        {
            var cursor = await client.Cursor.PostCursorAsync<string>(
                new PostCursorBody
                {
                    Query = CompletedLessonKeysAql,
                    BindVars = new Dictionary<string, object>
                    {
                        ["userId"] = $"users/{userKey}",
                        ["courseKey"] = courseKey
                    }
                });
            return cursor.Result.ToList();
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
