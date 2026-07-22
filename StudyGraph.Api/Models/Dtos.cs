namespace StudyGraph.Api.Models
{
    // ---------- Trả về từ repository (khớp RETURN của AQL Q1/Q2/Q3 — mục 5) ----------

    /// <summary>Kết quả Q1 — gợi ý cộng tác.</summary>
    public class CourseSuggestion
    {
        public Course Course { get; set; } = default!;
        public int SoBanHoc { get; set; }
        public double? AvgStars { get; set; }
    }

    /// <summary>Kết quả Q2 — khóa đã "mở khóa" (đủ điều kiện tiên quyết).</summary>
    public class UnlockedCourse
    {
        public Course Course { get; set; } = default!;
        public List<string> DaHocXong { get; set; } = new();   // các _id điều kiện tiên quyết
    }

    /// <summary>Kết quả Q3 — learning path.</summary>
    public class LearningPathStep
    {
        public Course Course { get; set; } = default!;
        public int Depth { get; set; }
    }

    // ---------- DTO cho API ----------

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
    }

    public class CourseDetailDto
    {
        public Course Course { get; set; } = default!;
        public List<Lesson> Lessons { get; set; } = new();     // sort theo Order
    }

    public class CourseUpsertRequest
    {
        public string? Key { get; set; }                        // slug đọc được, vd c-sql-101
        public string Title { get; set; } = default!;
        public string Category { get; set; } = default!;
        public int Level { get; set; } = 1;
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>1 dòng gợi ý sau khi Service trộn Q1 + Q2 và chấm điểm (mục 6).</summary>
    public class RecommendationItem
    {
        public string CourseKey { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Category { get; set; } = default!;
        public int Level { get; set; }
        public double Score { get; set; }
        public List<string> Reasons { get; set; } = new();      // hiển thị trên UI — vì sao máy gợi ý
    }

    public class ProgressItem
    {
        public Course Course { get; set; } = default!;
        public int Progress { get; set; }
        public string EnrolledAt { get; set; } = default!;
    }

    // Quiz: lấy đề phải GIẤU AnswerIndex
    public class QuizQuestionView
    {
        public string Q { get; set; } = default!;
        public List<string> Options { get; set; } = new();
    }

    public class QuizView
    {
        public string Key { get; set; } = default!;
        public string LessonKey { get; set; } = default!;
        public List<QuizQuestionView> Questions { get; set; } = new();
    }

    public class QuizSubmission
    {
        public List<int> Answers { get; set; } = new();         // index chọn theo thứ tự câu hỏi
    }

    public class QuizResult
    {
        public int Correct { get; set; }
        public int Total { get; set; }
        public int Score { get; set; }                          // 0..100, lưu vào edge completed
    }

    // Đăng nhập tối giản:
    // student nhập Email, nhận về UserKey để gắn vào header X-User-Key.
    public class LoginRequest
    {
        public string Email { get; set; } = default!;
    }

    public class LoginResponse
    {
        public string UserKey { get; set; } = default!;         // dùng làm giá trị X-User-Key
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
