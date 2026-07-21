using Newtonsoft.Json;
using StudyGraph.Api.Models;

namespace StudyGraph.Seeder;

/// <summary>Edge prerequisite_of: khóa nền → khóa nâng cao (không có thuộc tính thêm).</summary>
public class PrerequisiteOfEdge
{
    [JsonProperty("_from")] public string From { get; set; } = default!;
    [JsonProperty("_to")]   public string To   { get; set; } = default!;
}

/// <summary>
/// Toàn bộ dữ liệu sinh ra — tách khỏi phần ghi DB để tuần 4
/// StudyGraph.SqlImporter dùng lại ĐÚNG bộ dữ liệu này đổ vào SQL Server (mục 7).
/// </summary>
public class SeedData
{
    public List<User> Users { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public List<Lesson> Lessons { get; set; } = new();
    public List<Quiz> Quizzes { get; set; } = new();
    public List<EnrolledInEdge> EnrolledIn { get; set; } = new();
    public List<CompletedEdge> Completed { get; set; } = new();
    public List<PrerequisiteOfEdge> PrerequisiteOf { get; set; } = new();
    public List<RatedEdge> Rated { get; set; } = new();
}
