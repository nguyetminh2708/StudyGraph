using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using StudyGraph.Api.Models;

namespace StudyGraph.Api.Repositories;

/// <summary>
/// Nguyên tắc (mục 6): AQL sống trong Repository dưới dạng const string.
/// Bind variables luôn — không bao giờ nối chuỗi AQL (chống injection như SQL).
/// </summary>
public class RecommendationRepository(IArangoDBClient client)
{
    // Q1 — Gợi ý cộng tác: bạn học chung khóa đang học gì
    private const string CollaborativeAql = """
        // 2 bước ANY qua enrolled_in: tôi → khóa của tôi → người học chung
        // rồi OUTBOUND lấy khóa của họ mà tôi chưa ghi danh
        FOR nguoiChungKhoa IN 2..2 ANY @myUserId enrolled_in
          OPTIONS { uniqueVertices: "global", order: "bfs" }
          FILTER nguoiChungKhoa._id != @myUserId
          FOR khoa IN OUTBOUND nguoiChungKhoa enrolled_in
            FILTER khoa._id NOT IN @myCourseIds
            COLLECT c = khoa WITH COUNT INTO soBanHoc
            LET avgStars = FIRST(
              FOR r IN rated FILTER r._to == c._id
                COLLECT AGGREGATE avg = AVERAGE(r.Stars) RETURN avg
            )
            SORT soBanHoc DESC, avgStars DESC
            LIMIT 5
            RETURN { Course: c, SoBanHoc: soBanHoc, AvgStars: avgStars }
        """;

    // Q2 — Gợi ý theo lộ trình: khóa đã "mở khóa"
    private const string UnlockedAql = """
        // Khóa tôi chưa ghi danh, mà MỌI điều kiện tiên quyết đều nằm trong
        // danh sách khóa tôi đã hoàn thành
        FOR khoa IN courses
          FILTER khoa._id NOT IN @myCourseIds
          LET dieuKien = (FOR dk IN INBOUND khoa prerequisite_of RETURN dk._id)
          FILTER LENGTH(dieuKien) > 0
            AND LENGTH(MINUS(dieuKien, @myCompletedCourseIds)) == 0
          SORT khoa.Level, khoa.Title
          RETURN { Course: khoa, DaHocXong: dieuKien }
        """;

    public async Task<List<CourseSuggestion>> GetCollaborativeAsync(
        string userId, List<string> myCourseIds)
    {
        var cursor = await client.Cursor.PostCursorAsync<CourseSuggestion>(
            new PostCursorBody
            {
                Query = CollaborativeAql,
                BindVars = new Dictionary<string, object>
                {
                    ["myUserId"] = userId,
                    ["myCourseIds"] = myCourseIds
                }
            });
        return cursor.Result.ToList();
    }

    public async Task<List<UnlockedCourse>> GetUnlockedAsync(
        List<string> myCourseIds, List<string> myCompletedCourseIds)
    {
        var cursor = await client.Cursor.PostCursorAsync<UnlockedCourse>(
            new PostCursorBody
            {
                Query = UnlockedAql,
                BindVars = new Dictionary<string, object>
                {
                    ["myCourseIds"] = myCourseIds,
                    ["myCompletedCourseIds"] = myCompletedCourseIds
                }
            });
        return cursor.Result.ToList();
    }
}
