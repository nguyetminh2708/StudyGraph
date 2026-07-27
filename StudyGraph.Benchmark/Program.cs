using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.Data.SqlClient;
using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;
using StudyGraph.Benchmark;

// ============================================================================
// StudyGraph.Benchmark — bộ đo cho Chương 7
//
//   dotnet run -c Release -- --sf nho                 (mặc định)
//   dotnet run -c Release -- --sf vua --iter 100
//   dotnet run -c Release -- --sf lon --user u005 --course c-aspnet-301
//   dotnet run -c Release -- --sf nho --engine arango  (chỉ đo một hệ)
//
// Đo Q1, Q2, Q3 trên CẢ HAI hệ với cùng tham số, in bảng ra console và ghi
// bench-output/bench_<sf>.csv để dán vào bảng Mục 7.4.
//
// PHẢI chạy -c Release (Mục 7.1). Điều kiện: đã seed ArangoDB ở mức SF tương
// ứng và đã chạy SqlImporter cho cùng bộ dữ liệu đó.
// ============================================================================

var sf        = Arg("--sf")     ?? "nho";
var iter      = int.TryParse(Arg("--iter"), out var it) ? it : 100;
var warmup    = int.TryParse(Arg("--warmup"), out var wu) ? wu : 10;
var userKey   = Arg("--user")   ?? "u005";
var courseKey = Arg("--course") ?? "c-aspnet-301";
var engine    = Arg("--engine") ?? "both";        // arango | sql | both

var arangoUrl = Arg("--arango-url") ?? "http://localhost:8529";
var arangoDb  = Arg("--arango-db")  ?? "studygraph";
var arangoUser = Arg("--arango-user") ?? "root";
var arangoPass = Arg("--arango-pass") ?? "Study2026";
var sqlConn = Arg("--conn")
    ?? @"Server=(localdb)\MSSQLLocalDB;Database=StudyGraphSql;"
     + "Trusted_Connection=True;TrustServerCertificate=True";

#if DEBUG
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("CẢNH BÁO: đang chạy ở cấu hình Debug — số đo KHÔNG dùng được.");
Console.WriteLine("Chạy lại bằng: dotnet run -c Release -- --sf " + sf);
Console.ResetColor();
#endif

Console.WriteLine($"SF={sf}  iterations={iter}  warmup={warmup}  "
                + $"user={userKey}  course={courseKey}  engine={engine}");
Console.WriteLine();

var results = new List<BenchResult>();

// ---------------------------------------------------------------- ArangoDB ---
if (engine is "both" or "arango")
{
    using var transport = HttpApiTransport.UsingBasicAuth(
        new Uri(arangoUrl), arangoDb, arangoUser, arangoPass);
    using var client = new ArangoDBClient(transport);

    var version = (await client.Cursor.PostCursorAsync<string>(
        new PostCursorBody { Query = "RETURN VERSION()" })).Result.First();
    Console.WriteLine($"ArangoDB {version} @ {arangoUrl}/{arangoDb}");

    var userId = $"users/{userKey}";

    // Hai danh sách này được RecommendationService lấy trước khi gọi Q1/Q2
    // (Mục 4.5.1). Lấy sẵn ở đây để phép đo chỉ tính chi phí của chính Q1/Q2.
    var myCourseIds = (await client.Cursor.PostCursorAsync<string>(new PostCursorBody
    {
        Query = "FOR e IN enrolled_in FILTER e._from == @userId RETURN e._to",
        BindVars = new Dictionary<string, object> { ["userId"] = userId }
    })).Result.ToList();

    var myCompletedIds = (await client.Cursor.PostCursorAsync<string>(new PostCursorBody
    {
        Query = "FOR e IN enrolled_in FILTER e._from == @userId AND e.Progress == 100 "
              + "RETURN e._to",
        BindVars = new Dictionary<string, object> { ["userId"] = userId }
    })).Result.ToList();

    Console.WriteLine($"  {userKey}: {myCourseIds.Count} khóa đã ghi danh, "
                    + $"{myCompletedIds.Count} khóa hoàn thành 100%");

    results.Add(await BenchRunner.MeasureAsync("Q1", "ArangoDB", sf, userKey, async () =>
        await client.Cursor.PostCursorAsync<CourseSuggestion>(new PostCursorBody
        {
            Query = RecommendationRepository.CollaborativeAql,
            BindVars = new Dictionary<string, object>
            {
                ["myUserId"] = userId, ["myCourseIds"] = myCourseIds
            }
        }), warmup, iter));

    results.Add(await BenchRunner.MeasureAsync("Q2", "ArangoDB", sf, userKey, async () =>
        await client.Cursor.PostCursorAsync<UnlockedCourse>(new PostCursorBody
        {
            Query = RecommendationRepository.UnlockedAql,
            BindVars = new Dictionary<string, object>
            {
                ["myCourseIds"] = myCourseIds,
                ["myCompletedCourseIds"] = myCompletedIds
            }
        }), warmup, iter));

    results.Add(await BenchRunner.MeasureAsync("Q3", "ArangoDB", sf, courseKey, async () =>
        await client.Cursor.PostCursorAsync<LearningPathStep>(new PostCursorBody
        {
            Query = CourseRepository.LearningPathAql,
            BindVars = new Dictionary<string, object>
            {
                ["courseId"] = $"courses/{courseKey}"
            }
        }), warmup, iter));
}

// -------------------------------------------------------------- SQL Server ---
if (engine is "both" or "sql")
{
    await using var db = new SqlConnection(sqlConn);
    await db.OpenAsync();
    Console.WriteLine($"SQL Server @ {db.DataSource}/{db.Database}");

    // Cập nhật thống kê trước khi đo (nguyên tắc N4, Mục 6.1)
    await using (var stats = new SqlCommand(
        "EXEC sp_MSforeachtable 'UPDATE STATISTICS ? WITH FULLSCAN';", db)
        { CommandTimeout = 600 })
        await stats.ExecuteNonQueryAsync();

    results.Add(await BenchRunner.MeasureAsync("Q1", "SQLServer", sf, userKey,
        () => RunSql(db, Tsql.Q1Collaborative, ("@me", userKey)), warmup, iter));

    results.Add(await BenchRunner.MeasureAsync("Q2", "SQLServer", sf, userKey,
        () => RunSql(db, Tsql.Q2Unlocked, ("@me", userKey)), warmup, iter));

    results.Add(await BenchRunner.MeasureAsync("Q3", "SQLServer", sf, courseKey,
        () => RunSql(db, Tsql.Q3LearningPath,
                     ("@courseKey", courseKey), ("@maxDepth", 5)), warmup, iter));
}

// ------------------------------------------------------------------ báo cáo ---
Console.WriteLine();
Console.WriteLine($"{"Query",-5} {"Engine",-10} {"Param",-16} " +
                  $"{"p50",8} {"p95",8} {"p99",8} {"min",8} {"max",8}");
Console.WriteLine(new string('-', 78));
foreach (var r in results)
    Console.WriteLine($"{r.Query,-5} {r.Engine,-10} {r.Param,-16} " +
                      $"{r.P50,8:F3} {r.P95,8:F3} {r.P99,8:F3} {r.Min,8:F3} {r.Max,8:F3}");

// Hệ số tăng trưởng (bảng kết luận chính của Mục 7.4) chỉ tính được khi đã có
// kết quả của nhiều mức SF, nên ghi dồn vào một tệp CSV để so sánh sau.
Directory.CreateDirectory("bench-output");
var csv = Path.Combine("bench-output", $"bench_{sf}.csv");
await File.WriteAllLinesAsync(csv,
    new[] { BenchResult.CsvHeader }.Concat(results.Select(r => r.ToCsv())));
Console.WriteLine();
Console.WriteLine($"Đã ghi {csv}");
Console.WriteLine("Chạy đủ 3 mức (nho/vua/lon) rồi so p50 giữa các tệp để ra hệ số tăng trưởng.");

// ---------------------------------------------------------------------------
string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static async Task RunSql(SqlConnection db, string sql,
                         params (string Name, object Value)[] ps)
{
    await using var cmd = new SqlCommand(sql, db) { CommandTimeout = 600 };
    foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
    await using var reader = await cmd.ExecuteReaderAsync();
    // Đọc hết kết quả: nếu chỉ ExecuteReader mà không đọc, SQL Server có thể
    // chưa vật hoá xong và phép đo sẽ nhỏ hơn thực tế.
    while (await reader.ReadAsync()) { }
}
