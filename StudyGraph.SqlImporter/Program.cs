using System.Data;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using StudyGraph.Api.Models;

// ============================================================================
// StudyGraph.SqlImporter — nạp seed-output/*.json vào SQL Server (Mục 6.1)
//
//   dotnet run                              -> đọc ../seed-output
//   dotnet run -- --dir ..\seed-output      -> chỉ định thư mục khác
//   dotnet run -- --conn "Server=..."       -> chỉ định connection string
//
// Điều kiện: đã chạy sql\01_schema.sql để tạo database StudyGraphSql.
//
// Nguyên tắc N1 (Mục 6.1): cả hai hệ đọc từ CÙNG tám tệp JSON do cùng một
// đối tượng SeedData sinh ra, nên không có khả năng lệch dữ liệu.
// ============================================================================

var dir = ArgValue("--dir") ?? Path.Combine("..", "seed-output");
var conn = ArgValue("--conn")
           ?? @"Server=(localdb)\MSSQLLocalDB;Database=StudyGraphSql;"
            + "Trusted_Connection=True;TrustServerCertificate=True";

if (!Directory.Exists(dir))
{
    Console.Error.WriteLine($"Không thấy thư mục '{dir}'.");
    Console.Error.WriteLine("Chạy trước: cd ..\\StudyGraph.Seeder && dotnet run -- --json");
    return 1;
}

Console.WriteLine($"Đọc JSON từ : {Path.GetFullPath(dir)}");

var users    = Load<User>("users");
var courses  = Load<Course>("courses");
var lessons  = Load<Lesson>("lessons");
var quizzes  = Load<Quiz>("quizzes");
var enrolled = Load<EnrolledInEdge>("enrolled_in");
var completed = Load<CompletedEdge>("completed");
var prereq   = Load<PrereqRow>("prerequisite_of");
var rated    = Load<RatedEdge>("rated");

Console.WriteLine($"  users {users.Count}, courses {courses.Count}, lessons {lessons.Count}, "
                + $"quizzes {quizzes.Count}, enrolled_in {enrolled.Count}, "
                + $"completed {completed.Count}, prerequisite_of {prereq.Count}, rated {rated.Count}");

await using var db = new SqlConnection(conn);
await db.OpenAsync();
Console.WriteLine($"Kết nối    : {db.DataSource} / {db.Database}");

// ---- 1. Dọn sạch theo thứ tự NGƯỢC phụ thuộc khoá ngoại (lũy đẳng) ----------
// Bên ArangoDB thứ tự là "cạnh trước, đỉnh sau"; ở đây khoá ngoại được thi
// hành thật nên thứ tự là bắt buộc, không phải quy ước.
foreach (var t in new[] { "Ratings", "Prerequisites", "Completions", "Enrollments",
                          "QuizQuestions", "Quizzes", "Lessons", "CourseTags",
                          "Courses", "Users" })
{
    await Exec($"DELETE FROM dbo.{t};");
}
Console.WriteLine("Đã dọn 10 bảng.");

// ---- 2. Bảng thực thể --------------------------------------------------------
await Bulk("Users", users, new[] { "UserKey", "Name", "Email", "Role", "CreatedAt" },
    u => new object?[] { u.Key, u.Name, u.Email, u.Role, DateTime.Parse(u.CreatedAt) });

await Bulk("Courses", courses,
    new[] { "CourseKey", "Title", "Category", "Level", "Description" },
    c => new object?[] { c.Key, c.Title, c.Category, c.Level, c.Description });

// Tách mảng Course.Tags -> quan hệ riêng (bước chuẩn hoá 1NF, Mục 3.4)
var tagRows = courses.SelectMany(c => (c.Tags ?? new()).Distinct()
                                        .Select(t => (Course: c.Key, Tag: t))).ToList();
await Bulk("CourseTags", tagRows, new[] { "CourseKey", "Tag" },
    r => new object?[] { r.Course, r.Tag });

await Bulk("Lessons", lessons,
    new[] { "LessonKey", "CourseKey", "Title", "Ord", "Content" },
    l => new object?[] { l.Key, l.CourseKey, l.Title, l.Order, l.Content });

await Bulk("Quizzes", quizzes, new[] { "QuizKey", "LessonKey" },
    q => new object?[] { q.Key, q.LessonKey });

// Tách mảng Quiz.Questions -> quan hệ riêng. Cột Ord giữ thứ tự câu hỏi;
// QuestionId là khoá thay thế do SQL Server tự sinh (IDENTITY) — công thêm
// mà bên ArangoDB không cần vì câu hỏi lồng trong tài liệu quiz.
var questionRows = quizzes.SelectMany(q =>
        q.Questions.Select((x, i) => (Quiz: q.Key, Ord: i + 1, Q: x)))
    .ToList();
await Bulk("QuizQuestions", questionRows,
    new[] { "QuizKey", "Ord", "Q", "OptionA", "OptionB", "OptionC", "OptionD", "AnswerIndex" },
    r => new object?[]
    {
        r.Quiz, r.Ord, r.Q.Q,
        Opt(r.Q.Options, 0), Opt(r.Q.Options, 1), Opt(r.Q.Options, 2), Opt(r.Q.Options, 3),
        r.Q.AnswerIndex
    });

// ---- 3. Bảng kết -------------------------------------------------------------
// Bóc tiền tố collection khỏi _from/_to: "users/u001" -> "u001".
// Định danh của ArangoDB MANG THEO tên collection nên một cạnh tự mô tả được
// nó nối cái gì; trong lược đồ quan hệ thông tin đó nằm ở tên cột và khoá ngoại.
await Bulk("Enrollments", enrolled,
    new[] { "UserKey", "CourseKey", "EnrolledAt", "Progress" },
    e => new object?[] { Key(e.From), Key(e.To), DateTime.Parse(e.EnrolledAt), e.Progress });

await Bulk("Completions", completed,
    new[] { "UserKey", "LessonKey", "CompletedAt", "Score" },
    e => new object?[] { Key(e.From), Key(e.To), DateTime.Parse(e.CompletedAt),
                         (object?)e.Score ?? DBNull.Value });

await Bulk("Prerequisites", prereq, new[] { "PrereqCourseKey", "CourseKey" },
    e => new object?[] { Key(e.From), Key(e.To) });

await Bulk("Ratings", rated, new[] { "UserKey", "CourseKey", "Stars", "Comment" },
    e => new object?[] { Key(e.From), Key(e.To), e.Stars, (object?)e.Comment ?? DBNull.Value });

// ---- 4. Cập nhật thống kê — BẮT BUỘC trước khi đo (nguyên tắc N4) -----------
Console.WriteLine("Cập nhật thống kê...");
await Exec("EXEC sp_MSforeachtable 'UPDATE STATISTICS ? WITH FULLSCAN';");

Console.WriteLine("Nhập xong. Kiểm tra nhanh:");
await Report("SELECT COUNT(*) FROM Users",         "Users");
await Report("SELECT COUNT(*) FROM Courses",       "Courses");
await Report("SELECT COUNT(*) FROM Enrollments",   "Enrollments");
await Report("SELECT COUNT(*) FROM Completions",   "Completions");
await Report("SELECT COUNT(*) FROM Prerequisites", "Prerequisites");
await Report("SELECT COUNT(*) FROM Ratings",       "Ratings");
Console.WriteLine("Bước tiếp theo: sqlcmd -d StudyGraphSql -i ..\\sql\\02_queries_Q1_Q2_Q3.sql");
return 0;

// ---------------------------------------------------------------------------
string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

List<T> Load<T>(string name)
{
    var path = Path.Combine(dir, name + ".json");
    if (!File.Exists(path)) throw new FileNotFoundException($"Thiếu tệp {path}");
    return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
}

static string Key(string id) => id[(id.IndexOf('/') + 1)..];
static object Opt(List<string> o, int i) => i < o.Count ? o[i] : "";

async Task Exec(string sql)
{
    await using var cmd = new SqlCommand(sql, db) { CommandTimeout = 600 };
    await cmd.ExecuteNonQueryAsync();
}

async Task Report(string sql, string label)
{
    await using var cmd = new SqlCommand(sql, db);
    Console.WriteLine($"  {label,-14} {await cmd.ExecuteScalarAsync()}");
}

async Task Bulk<T>(string table, IReadOnlyList<T> rows, string[] columns,
                   Func<T, object?[]> project)
{
    var dt = new DataTable();
    foreach (var c in columns) dt.Columns.Add(c);
    foreach (var r in rows) dt.Rows.Add(project(r));

    using var bulk = new SqlBulkCopy(db)
    {
        DestinationTableName = "dbo." + table,
        BatchSize = 5_000,
        BulkCopyTimeout = 600
    };
    foreach (var c in columns) bulk.ColumnMappings.Add(c, c);
    await bulk.WriteToServerAsync(dt);
    Console.WriteLine($"  {table,-14} {rows.Count}");
}

/// <summary>
/// prerequisite_of không có thuộc tính ngoài _from/_to. Lớp tương ứng bên
/// Seeder (PrerequisiteOfEdge) nằm trong project Seeder nên khai báo lại ở đây
/// để SqlImporter không phải tham chiếu chéo sang project đó.
/// </summary>
public class PrereqRow
{
    [JsonProperty("_from")] public string From { get; set; } = default!;
    [JsonProperty("_to")]   public string To   { get; set; } = default!;
}
