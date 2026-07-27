using Newtonsoft.Json.Linq;

namespace StudyGraph.Seeder;

/// <summary>
/// Cấu hình kết nối ArangoDB cho Seeder, lấy theo thứ tự ưu tiên:
///   1. Tham số dòng lệnh  --url / --db / --user / --pass
///   2. Mục "Arango" trong StudyGraph.Api/appsettings.json (hoặc appsettings.json
///      cạnh tệp thực thi) — để Seeder và API dùng CÙNG một cấu hình
///   3. Giá trị mặc định của môi trường phát triển
///
/// Trước đây Program.cs hardcode url/db/user/password, nên sửa appsettings.json
/// chỉ có tác dụng với API mà không có tác dụng với Seeder — hai bên dễ trỏ vào
/// hai database khác nhau mà không có dấu hiệu nào.
///
/// Chú ý: khoá trong appsettings.json của repo này là "User" (không phải
/// "Username") — khớp với cách Program.cs của API đọc cfg["User"].
/// </summary>
public record ArangoConfig(string Url, string Database, string User, string Password)
{
    private const string DefaultUrl = "http://localhost:8529";
    private const string DefaultDatabase = "studygraph";
    private const string DefaultUser = "root";
    private const string DefaultPassword = "Study2026";

    public static ArangoConfig Load(string[] args)
    {
        var json = FindSection();

        return new ArangoConfig(
            Arg(args, "--url")  ?? Val(json, "Url")      ?? DefaultUrl,
            Arg(args, "--db")   ?? Val(json, "Database") ?? DefaultDatabase,
            Arg(args, "--user") ?? Val(json, "User")     ?? DefaultUser,
            Arg(args, "--pass") ?? Val(json, "Password") ?? DefaultPassword);
    }

    private static JObject? FindSection()
    {
        // Chạy bằng `dotnet run` thì cwd là thư mục project, nên đường dẫn tương
        // đối tới project API là ..\StudyGraph.Api. Khi chạy tệp .exe đã build,
        // appsettings.json nằm ngay cạnh tệp thực thi.
        var candidates = new[]
        {
            Path.Combine("..", "StudyGraph.Api", "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            "appsettings.json"
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                if (root["Arango"] is JObject section) return section;
            }
            catch
            {
                // Tệp cấu hình hỏng thì bỏ qua và dùng mặc định — Seeder không nên
                // chết vì lỗi cấu hình khi vẫn có giá trị mặc định dùng được.
            }
        }
        return null;
    }

    private static string? Val(JObject? section, string key)
    {
        var v = section?[key]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
