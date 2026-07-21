using Newtonsoft.Json;

namespace StudyGraph.Seeder;

/// <summary>
/// Ghi bộ dữ liệu ra seed-output/*.json — StudyGraph.SqlImporter (tuần 4) đọc đúng
/// bộ JSON này đổ vào SQL Server bằng SqlBulkCopy → 2 hệ CÙNG một bộ dữ liệu (mục 7).
/// </summary>
public static class JsonExporter
{
    public static void Write(SeedData data, string dir)
    {
        Directory.CreateDirectory(dir);
        Dump(dir, "users", data.Users);
        Dump(dir, "courses", data.Courses);
        Dump(dir, "lessons", data.Lessons);
        Dump(dir, "quizzes", data.Quizzes);
        Dump(dir, "enrolled_in", data.EnrolledIn);
        Dump(dir, "completed", data.Completed);
        Dump(dir, "prerequisite_of", data.PrerequisiteOf);
        Dump(dir, "rated", data.Rated);
    }

    private static void Dump<T>(string dir, string name, List<T> items)
        => File.WriteAllText(Path.Combine(dir, $"{name}.json"),
            JsonConvert.SerializeObject(items, Formatting.Indented));
}
