using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.Transport.Http;
using StudyGraph.Seeder;

// ---- Tham số: scale factor (mục 7) + tùy chọn xuất JSON cho SqlImporter ----
//   dotnet run                          -> SF-nhỏ  (50 users / 12 courses — demo app)
//   dotnet run -- --sf vua              -> SF-vừa  (5.000 users / 198 courses)
//   dotnet run -- --sf lon              -> SF-lớn  (50.000 users / 999 courses)
//   dotnet run -- --json                -> ghi thêm seed-output/*.json
var sf = args.SkipWhile(a => a != "--sf").Skip(1).FirstOrDefault() ?? "nho";
var exportJson = args.Contains("--json");

var (users, courses) = sf switch
{
    // Tham số thứ hai là SỐ KHÓA MONG MUỐN. BuildTracks tính
    // trackCount = max(4, mongMuon / 3) rồi sinh đúng 3 khóa mỗi track, nên số
    // khóa thực tế bị làm tròn xuống bội số của 3: 200 -> 198, 1.000 -> 999.
    "vua" => (5_000, 200),
    "lon" => (50_000, 1_000),
    _ => (50, 12)
};

Console.WriteLine($"Sinh dữ liệu SF-{sf}: {users} users / {courses} courses (Random(42) cố định)...");
var data = new DataGenerator(users, courses).Generate();

if (exportJson)
{
    JsonExporter.Write(data, "seed-output");
    Console.WriteLine("Đã ghi seed-output/*.json (cho StudyGraph.SqlImporter).");
}

// ---- Kết nối ----
// Đọc từ StudyGraph.Api/appsettings.json để Seeder và API dùng CÙNG một cấu
// hình. Ghi đè được bằng tham số dòng lệnh:
//   dotnet run -- --url http://localhost:8529 --db studygraph --user root --pass ...
var cfg = ArangoConfig.Load(args);
Console.WriteLine($"Kết nối: {cfg.Url} / {cfg.Database} (user {cfg.User})");

using var transport = HttpApiTransport.UsingBasicAuth(
    new Uri(cfg.Url), cfg.Database, cfg.User, cfg.Password);
using var client = new ArangoDBClient(transport);

// ---- 1. Xóa sạch để chạy lại được nhiều lần (idempotent) — edge trước, document sau ----
foreach (var col in new[] { "rated", "completed", "enrolled_in", "prerequisite_of",
                            "quizzes", "lessons", "courses", "users" })
{
    await client.Cursor.PostCursorAsync<object>(new PostCursorBody
    {
        Query = "FOR d IN @@col REMOVE d IN @@col",
        BindVars = new Dictionary<string, object> { ["@col"] = col }
    });
    Console.WriteLine($"  đã dọn {col}");
}

// ---- 2. Insert documents theo batch ----
await InsertChunked("users", data.Users);
await InsertChunked("courses", data.Courses);
await InsertChunked("lessons", data.Lessons);
await InsertChunked("quizzes", data.Quizzes);

// ---- 3. Insert edges (_from/_to là _id đầy đủ "users/u001") ----
await InsertChunked("enrolled_in", data.EnrolledIn);
await InsertChunked("completed", data.Completed);
await InsertChunked("prerequisite_of", data.PrerequisiteOf);
await InsertChunked("rated", data.Rated);

Console.WriteLine($"Seed xong: {data.Courses.Count} courses, {data.Lessons.Count} lessons, " +
                  $"{data.Users.Count} users, {data.EnrolledIn.Count} enrolled_in, " +
                  $"{data.Completed.Count} completed, {data.Rated.Count} rated.");

// PostDocumentsAsync gửi cả list 1 lần — chia nhỏ 1.000/doc mỗi request cho SF-lớn
async Task InsertChunked<T>(string collection, List<T> items)
{
    const int chunkSize = 1_000;
    for (var i = 0; i < items.Count; i += chunkSize)
        await client.Document.PostDocumentsAsync(collection,
            items.Skip(i).Take(chunkSize).ToList());
    Console.WriteLine($"  {collection}: {items.Count}");
}
