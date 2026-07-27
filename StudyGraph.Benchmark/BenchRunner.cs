using System.Diagnostics;

namespace StudyGraph.Benchmark;

/// <summary>Một dòng kết quả đo — khớp các cột của bảng ở Mục 7.4.</summary>
public record BenchResult(
    string Query, string Engine, string Scale, string Param,
    double P50, double P95, double P99, double Min, double Max, int N)
{
    public static string CsvHeader =>
        "Query,Engine,Scale,Param,P50_ms,P95_ms,P99_ms,Min_ms,Max_ms,N";

    public string ToCsv() =>
        $"{Query},{Engine},{Scale},{Param}," +
        $"{P50:F3},{P95:F3},{P99:F3},{Min:F3},{Max:F3},{N}";
}

public static class BenchRunner
{
    /// <summary>
    /// Quy trình đo của Mục 7.3:
    ///   - Làm nóng (mặc định 10 lần, LOẠI BỎ kết quả) để loại chi phí JIT của
    ///     .NET, chi phí biên dịch kế hoạch truy vấn, và chi phí nạp trang dữ
    ///     liệu từ đĩa vào bộ đệm. Bỏ bước này sẽ làm lần chạy đầu tiên chậm
    ///     hơn các lần sau hàng chục lần và kéo lệch trung bình.
    ///   - Đo 100 lần, ghi thời gian TỪNG LẦN bằng Stopwatch.GetTimestamp()
    ///     (không dùng DateTime.Now — độ phân giải ~15 ms trên Windows, thô hơn
    ///     cả thời gian cần đo).
    ///   - Báo cáo trung vị làm số chính vì phân bố lệch phải mạnh (GC, hoạt
    ///     động nền của OS, checkpoint của CSDL tạo giá trị ngoại lai).
    ///     p95/p99 vẫn phải báo cáo vì độ trễ phần đuôi là thứ người dùng cảm nhận.
    /// </summary>
    public static async Task<BenchResult> MeasureAsync(
        string query, string engine, string scale, string param,
        Func<Task> action, int warmup = 10, int iterations = 100)
    {
        for (var i = 0; i < warmup; i++) await action();

        var ticks = new long[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var t0 = Stopwatch.GetTimestamp();
            await action();
            ticks[i] = Stopwatch.GetTimestamp() - t0;
        }

        var ms = ticks.Select(t => t * 1000.0 / Stopwatch.Frequency)
                      .OrderBy(x => x)
                      .ToArray();

        double Pct(double p) =>
            ms[(int)Math.Min(ms.Length - 1, Math.Floor(p * ms.Length))];

        return new BenchResult(query, engine, scale, param,
            Pct(0.50), Pct(0.95), Pct(0.99), ms[0], ms[^1], iterations);
    }
}
