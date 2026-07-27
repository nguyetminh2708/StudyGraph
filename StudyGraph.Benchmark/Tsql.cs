namespace StudyGraph.Benchmark;

/// <summary>
/// Ba truy vấn SQL đối chứng (Chương 6). Giữ NGUYÊN VĂN với
/// sql\02_queries_Q1_Q2_Q3.sql — nếu sửa một bên phải sửa cả bên kia.
/// (Bên AQL không có vấn đề này vì Benchmark tham chiếu trực tiếp const
/// của Repository, nên chỉ có một nguồn duy nhất.)
/// </summary>
public static class Tsql
{
    /// <summary>Q1 — gợi ý cộng tác. 3 CTE mô phỏng 3 bước traversal.</summary>
    public const string Q1Collaborative = """
        WITH MyCourses AS (
            SELECT CourseKey FROM Enrollments WHERE UserKey = @me
        ),
        Peers AS (
            SELECT DISTINCT e.UserKey
            FROM Enrollments e
            INNER JOIN MyCourses m ON m.CourseKey = e.CourseKey
            WHERE e.UserKey <> @me
        ),
        Candidates AS (
            SELECT e.CourseKey, COUNT(DISTINCT e.UserKey) AS SoBanHoc
            FROM Enrollments e
            INNER JOIN Peers p ON p.UserKey = e.UserKey
            WHERE NOT EXISTS (SELECT 1 FROM MyCourses m
                              WHERE m.CourseKey = e.CourseKey)
            GROUP BY e.CourseKey
        )
        SELECT TOP 5
               c.CourseKey, c.Title, c.Category, c.Level,
               cand.SoBanHoc, r.AvgStars
        FROM Candidates cand
        INNER JOIN Courses c ON c.CourseKey = cand.CourseKey
        OUTER APPLY (
            SELECT AVG(CAST(Stars AS FLOAT)) AS AvgStars
            FROM Ratings rt WHERE rt.CourseKey = cand.CourseKey
        ) r
        ORDER BY cand.SoBanHoc DESC, r.AvgStars DESC;
        """;

    /// <summary>
    /// Q2 — khóa đã mở khóa. Điều kiện P(c) \ D(u) = {} phải diễn đạt bằng
    /// NOT EXISTS lồng trong NOT EXISTS (phủ định của phủ định), vì SQL không
    /// có phép hiệu tập hợp dùng được như biểu thức trong WHERE tương quan.
    /// </summary>
    public const string Q2Unlocked = """
        WITH MyCourses AS (
            SELECT CourseKey FROM Enrollments WHERE UserKey = @me
        ),
        MyCompleted AS (
            SELECT CourseKey FROM Enrollments
            WHERE UserKey = @me AND Progress = 100
        )
        SELECT c.CourseKey, c.Title, c.Category, c.Level
        FROM Courses c
        WHERE NOT EXISTS (SELECT 1 FROM MyCourses m
                          WHERE m.CourseKey = c.CourseKey)
          AND EXISTS (SELECT 1 FROM Prerequisites p
                      WHERE p.CourseKey = c.CourseKey)
          AND NOT EXISTS (
                SELECT 1 FROM Prerequisites p
                WHERE p.CourseKey = c.CourseKey
                  AND NOT EXISTS (SELECT 1 FROM MyCompleted d
                                  WHERE d.CourseKey = p.PrereqCourseKey)
              )
        ORDER BY c.Level, c.Title;
        """;

    /// <summary>
    /// Q3 — learning path. Đối chứng của 3 dòng AQL; SQL phải tự cài đặt bộ đếm
    /// độ sâu, sổ ghi đường đi chống chu trình, phép loại trùng và bộ đếm dừng.
    /// </summary>
    public const string Q3LearningPath = """
        WITH PathCte AS (
            SELECT p.PrereqCourseKey AS CourseKey,
                   1 AS Depth,
                   CAST('/' + p.PrereqCourseKey + '/' AS VARCHAR(4000)) AS PathStr
            FROM Prerequisites p
            WHERE p.CourseKey = @courseKey
            UNION ALL
            SELECT p.PrereqCourseKey,
                   pc.Depth + 1,
                   CAST(pc.PathStr + p.PrereqCourseKey + '/' AS VARCHAR(4000))
            FROM Prerequisites p
            INNER JOIN PathCte pc ON p.CourseKey = pc.CourseKey
            WHERE pc.Depth < @maxDepth
              AND pc.PathStr NOT LIKE '%/' + p.PrereqCourseKey + '/%'
        )
        SELECT c.CourseKey, c.Title, c.Category, c.Level,
               MAX(pc.Depth) AS Depth
        FROM PathCte pc
        INNER JOIN Courses c ON c.CourseKey = pc.CourseKey
        GROUP BY c.CourseKey, c.Title, c.Category, c.Level
        ORDER BY Depth DESC, c.Title
        OPTION (MAXRECURSION 0);
        """;
}
