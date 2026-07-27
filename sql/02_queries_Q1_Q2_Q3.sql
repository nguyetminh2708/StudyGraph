-- ===========================================================================
-- StudyGraph — ba truy vấn SQL đối chứng (Chương 6 của tiểu luận)
--
-- Chạy: sqlcmd -S "(localdb)\MSSQLLocalDB" -d StudyGraphSql -i sql\02_queries_Q1_Q2_Q3.sql
--
-- Ba truy vấn này tương đương NGỮ NGHĨA với Q1/Q2/Q3 trong AQL
-- (RecommendationRepository.cs và CourseRepository.cs).
-- Tham số mặc định dùng u005 và c-aspnet-301 — đúng hai ví dụ trong tiểu luận.
-- ===========================================================================
USE StudyGraphSql;
GO

-- Bắt buộc trước khi đo hiệu năng (nguyên tắc N4, Mục 6.1): bộ tối ưu hoá
-- dựa vào thống kê để chọn kế hoạch; thống kê lỗi thời sau một lần nhập lớn
-- sẽ cho kế hoạch tệ và tạo kết quả có lợi cho ArangoDB một cách giả tạo.
EXEC sp_MSforeachtable 'UPDATE STATISTICS ? WITH FULLSCAN';
GO

-- ===========================================================================
-- Q1-SQL — Gợi ý cộng tác
-- Tương ứng AQL: FOR ... IN 2..2 ANY @myUserId enrolled_in ... (13 dòng)
-- Ba CTE mô phỏng ba bước của traversal.
-- ===========================================================================
DECLARE @me VARCHAR(20) = 'u005';

WITH MyCourses AS (          -- bước 1: OUTBOUND từ tôi tới khóa của tôi
    SELECT CourseKey
    FROM Enrollments
    WHERE UserKey = @me
),
Peers AS (                   -- bước 2: INBOUND từ khóa tới người học chung
    -- DISTINCT ở đây chính là vai trò của OPTIONS { uniqueVertices: "global" }.
    -- Khác biệt: bên AQL đó là tùy chọn khai báo có tên tự mô tả; bên SQL là
    -- từ khoá mà người viết phải TỰ NHẬN RA là cần — quên nó cho kết quả sai
    -- mà không báo lỗi.
    SELECT DISTINCT e.UserKey
    FROM Enrollments e
    INNER JOIN MyCourses m ON m.CourseKey = e.CourseKey
    WHERE e.UserKey <> @me
),
Candidates AS (              -- bước 3: OUTBOUND từ người học chung tới khóa của họ
    SELECT e.CourseKey,
           COUNT(DISTINCT e.UserKey) AS SoBanHoc
    FROM Enrollments e
    INNER JOIN Peers p ON p.UserKey = e.UserKey
    -- NOT EXISTS thay vì NOT IN: với NOT IN, một giá trị NULL trong truy vấn
    -- con làm toàn bộ điều kiện thành UNKNOWN và kết quả rỗng (cạm bẫy NULL
    -- cổ điển của SQL). Bên AQL không có logic ba giá trị nên không có cạm bẫy này.
    WHERE NOT EXISTS (SELECT 1 FROM MyCourses m
                      WHERE m.CourseKey = e.CourseKey)
    GROUP BY e.CourseKey
)
SELECT TOP 5
       c.CourseKey, c.Title, c.Category, c.Level,
       cand.SoBanHoc,
       r.AvgStars
FROM Candidates cand
INNER JOIN Courses c ON c.CourseKey = cand.CourseKey
-- OUTER (không phải CROSS) APPLY để khóa chưa có đánh giá vẫn xuất hiện với
-- AvgStars = NULL — tương ứng chính xác hành vi của FIRST() bên AQL.
-- CAST(... AS FLOAT) là BẮT BUỘC: AVG trên INT trong SQL Server chia NGUYÊN,
-- nên AVG của {4,5} cho ra 4 chứ không phải 4,5 — sai lệch âm thầm.
OUTER APPLY (
    SELECT AVG(CAST(Stars AS FLOAT)) AS AvgStars
    FROM Ratings rt
    WHERE rt.CourseKey = cand.CourseKey
) r
ORDER BY cand.SoBanHoc DESC, r.AvgStars DESC;
GO

-- Kết quả kỳ vọng với u005 trên bộ dữ liệu SF-nhỏ (khớp Mục 5.1 của tiểu luận):
--   c-html-101      HTML & CSS       SoBanHoc=13  AvgStars=4.50
--   c-js-201        JavaScript       SoBanHoc= 5  AvgStars=3.00
--   c-react-301     React            SoBanHoc= 3  AvgStars=3.50
--   c-dbdesign-301  Thiết kế CSDL    SoBanHoc= 2  AvgStars=3.50
--   c-aspnet-301    ASP.NET Core     SoBanHoc= 2  AvgStars=NULL


-- ===========================================================================
-- Q2-SQL — Gợi ý theo lộ trình đã mở khóa
-- Tương ứng AQL: LENGTH(MINUS(dieuKien, @myCompletedCourseIds)) == 0  (1 dòng)
--
-- Điều kiện toán học:  P(c) <> {} AND P(c) \ D(u) = {}
-- Bên SQL, phép hiệu tập hợp trên hai tập con TƯƠNG QUAN không có cách diễn
-- đạt trực tiếp (EXCEPT là toán tử trên tập kết quả của câu lệnh, không dùng
-- được như biểu thức trong WHERE), nên phải chuyển thành NOT EXISTS lồng
-- trong NOT EXISTS — phủ định của phủ định.
-- ===========================================================================
DECLARE @me2 VARCHAR(20) = 'u005';

WITH MyCourses AS (
    SELECT CourseKey FROM Enrollments WHERE UserKey = @me2
),
MyCompleted AS (
    SELECT CourseKey FROM Enrollments
    WHERE UserKey = @me2 AND Progress = 100
)
SELECT c.CourseKey, c.Title, c.Category, c.Level
FROM Courses c
WHERE NOT EXISTS (SELECT 1 FROM MyCourses m WHERE m.CourseKey = c.CourseKey)
  -- Loại khóa không có tiên quyết. Nếu bỏ, mọi khóa mức 1 đều thoả điều kiện
  -- kế tiếp một cách tầm thường (MINUS({}, X) = {}) và làm loãng danh sách.
  AND EXISTS (SELECT 1 FROM Prerequisites p WHERE p.CourseKey = c.CourseKey)
  -- "Không tồn tại một tiên quyết của c mà không tồn tại trong tập đã hoàn thành."
  AND NOT EXISTS (
        SELECT 1
        FROM Prerequisites p
        WHERE p.CourseKey = c.CourseKey
          AND NOT EXISTS (SELECT 1 FROM MyCompleted d
                          WHERE d.CourseKey = p.PrereqCourseKey)
      )
ORDER BY c.Level, c.Title;
GO

-- Kết quả kỳ vọng với u005 (đã hoàn thành 100%: c-cs-101, c-sql-201):
--   c-dbdesign-301  Thiết kế CSDL  Database  3
-- c-aspnet-301 BỊ LOẠI đúng vì nó có HAI tiên quyết (c-cs-201 ở 60%,
-- c-sql-101 ở 80%) — đây là trường hợp kiểm thử then chốt mà cạnh chéo
-- c-sql-101 -> c-aspnet-301 được đưa vào dữ liệu để phát hiện.


-- ===========================================================================
-- Q3-SQL — Learning path bằng recursive CTE
-- Tương ứng AQL: FOR v,e,p IN 1..5 INBOUND @courseId prerequisite_of  (3 dòng)
--
-- Bốn thành phần SQL phải TỰ cài đặt trong khi AQL có sẵn:
--   1. Bộ đếm độ sâu        (AQL: LENGTH(p.edges))
--   2. Chống thăm lại đỉnh  (AQL: OPTIONS { uniqueVertices: "global" })
--   3. Loại trùng + chọn độ sâu
--   4. Bộ đếm dừng          (AQL: 1..5)
-- ===========================================================================
DECLARE @courseKey VARCHAR(40) = 'c-aspnet-301';
DECLARE @maxDepth  INT = 5;

WITH PathCte AS (
    -- (1) Anchor member: các tiên quyết trực tiếp, độ sâu 1
    SELECT p.PrereqCourseKey AS CourseKey,
           1 AS Depth,
           CAST('/' + p.PrereqCourseKey + '/' AS VARCHAR(4000)) AS PathStr
    FROM Prerequisites p
    WHERE p.CourseKey = @courseKey

    UNION ALL

    -- (2) Recursive member: tiên quyết của tiên quyết
    SELECT p.PrereqCourseKey,
           pc.Depth + 1,
           CAST(pc.PathStr + p.PrereqCourseKey + '/' AS VARCHAR(4000))
    FROM Prerequisites p
    INNER JOIN PathCte pc ON p.CourseKey = pc.CourseKey
    WHERE pc.Depth < @maxDepth
      -- Chống chu trình: tự mang theo đường đi rồi so chuỗi.
      -- Bốn nhược điểm: so chuỗi thay vì so khoá (không dùng được chỉ mục);
      -- VARCHAR(4000) giới hạn ~97 bước, vượt quá thì chuỗi bị cắt ÂM THẦM và
      -- phép kiểm tra SAI mà không báo lỗi; phải bọc khoá bằng '/' ở cả hai
      -- đầu, nếu viết '%' + key + '%' thì c-sql-101 khớp sai với c-sql-1011;
      -- và nó chỉ tương đương uniqueVertices:"path", KHÔNG phải "global".
      AND pc.PathStr NOT LIKE '%/' + p.PrereqCourseKey + '/%'
)
-- (3) Vì bước chống chu trình không cho tính duy nhất toàn cục, cần GROUP BY
-- với MAX(Depth). Điều thú vị: cách này ĐÚNG HƠN phiên bản AQL đang chạy —
-- uniqueVertices:"global" với BFS cho độ sâu NHỎ NHẤT, còn thứ tự học đúng
-- cần độ sâu LỚN NHẤT (hạn chế H-2).
SELECT c.CourseKey, c.Title, c.Category, c.Level,
       MAX(pc.Depth) AS Depth
FROM PathCte pc
INNER JOIN Courses c ON c.CourseKey = pc.CourseKey
GROUP BY c.CourseKey, c.Title, c.Category, c.Level
-- Depth lớn = phải học trước tiên
ORDER BY Depth DESC, c.Title
-- (4) Tắt giới hạn mặc định 100 vòng để pc.Depth < @maxDepth là bộ đếm dừng
-- duy nhất. Nếu giữ mặc định và chạm ngưỡng, SQL Server BÁO LỖI và huỷ toàn
-- bộ truy vấn — trong khi ArangoDB trả kết quả bộ phận tới độ sâu đã đi được.
OPTION (MAXRECURSION 0);
GO

-- Kết quả kỳ vọng với c-aspnet-301 (khớp Mục 5.3 của tiểu luận):
--   c-cs-101    C# căn bản     Depth=2   <- học trước tiên
--   c-cs-201    C# nâng cao    Depth=1
--   c-sql-101   SQL căn bản    Depth=1
