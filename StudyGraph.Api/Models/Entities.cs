using Newtonsoft.Json;

namespace StudyGraph.Api.Models
{
    // Field đặt PascalCase khớp class C# (driver giữ nguyên tên property).
    // Các field hệ thống _key/_id/_from/_to là của ArangoDB — map qua [JsonProperty].

    public class Course
    {
        [JsonProperty("_key")] public string Key { get; set; } = default!;
        [JsonProperty("_id")] public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Category { get; set; } = default!;   // "Database"|"Backend"|"Frontend"|"DevOps"
        public int Level { get; set; }                      // 1..3
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = new();
    }

    public class Lesson
    {
        [JsonProperty("_key")] public string Key { get; set; } = default!;
        [JsonProperty("_id")] public string Id { get; set; } = default!;
        public string CourseKey { get; set; } = default!;
        public string Title { get; set; } = default!;
        public int Order { get; set; }
        public string Content { get; set; } = "";           // markdown
    }

    public class QuizQuestion
    {
        public string Q { get; set; } = default!;
        public List<string> Options { get; set; } = new();  // 4 lựa chọn
        public int AnswerIndex { get; set; }                 // KHÔNG trả về client khi lấy đề
    }

    public class Quiz
    {
        [JsonProperty("_key")] public string Key { get; set; } = default!;
        [JsonProperty("_id")] public string Id { get; set; } = default!;
        public string LessonKey { get; set; } = default!;
        public List<QuizQuestion> Questions { get; set; } = new();
    }

    public class User
    {
        [JsonProperty("_key")] public string Key { get; set; } = default!;
        [JsonProperty("_id")] public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;       // unique index
        public string Role { get; set; } = "student";       // "student"|"admin"
        public string CreatedAt { get; set; } = default!;   // ISO string
    }
}
