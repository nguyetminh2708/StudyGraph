using StudyGraph.Api.Models;

namespace StudyGraph.Seeder;

/// <summary>
/// Sinh dữ liệu mẫu theo chiến lược mục 4 artifact: gợi ý cộng tác chỉ hay khi
/// dữ liệu CÓ CỤM — nên không random đều mà tạo 3 persona rõ rệt.
/// Random(42) cố định + ngày neo cố định → chạy lại bao nhiêu lần cũng ra đúng
/// một bộ số liệu (tái lập được cho benchmark tuần 4).
/// </summary>
public class DataGenerator(int scaleUsers = 50, int scaleCourses = 12)
{
    private readonly Random _rng = new(42);
    private static readonly DateTime Anchor = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    // 4 track gốc, mỗi track 3 khóa (Level 1→3) — đúng bản đồ prerequisite mục 3
    private static readonly (string Track, string Category, string[] Keys, string[] Titles)[] BaseTracks =
    {
        ("data",  "Database", new[] { "c-sql-101", "c-sql-201", "c-dbdesign-301" },
                              new[] { "SQL căn bản", "SQL nâng cao", "Thiết kế CSDL" }),
        ("be",    "Backend",  new[] { "c-cs-101", "c-cs-201", "c-aspnet-301" },
                              new[] { "C# căn bản", "C# nâng cao", "ASP.NET Core" }),
        ("fe",    "Frontend", new[] { "c-html-101", "c-js-201", "c-react-301" },
                              new[] { "HTML & CSS", "JavaScript", "React" }),
        ("infra", "DevOps",   new[] { "c-linux-101", "c-docker-201", "c-aws-301" },
                              new[] { "Linux căn bản", "Docker", "AWS" }),
    };

    private static readonly string[] Ho = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Đặng", "Bùi" };
    private static readonly string[] Ten = { "An", "Bình", "Chi", "Dũng", "Hà", "Khánh", "Linh", "Minh", "Nam", "Phương", "Quân", "Thảo" };

    public SeedData Generate()
    {
        var data = new SeedData();

        // ---- 1. Courses + prerequisite chains (theo track, mỗi track 3 khóa) ----
        var tracks = BuildTracks(scaleCourses, data);

        // ---- 2. Lessons (5/khóa) + Quizzes (1-2/khóa) ----
        foreach (var course in data.Courses)
        {
            var suffix = course.Key[2..];                     // "c-sql-101" -> "sql-101"
            for (var i = 1; i <= 5; i++)
            {
                data.Lessons.Add(new Lesson
                {
                    Key = $"l-{suffix}-{i:00}",
                    CourseKey = course.Key,
                    Title = $"Bài {i}: {course.Title}",
                    Order = i,
                    Content = $"# Bài {i}\nNội dung bài học {i} của khóa {course.Title}."
                });
            }
            var quizCount = 1 + _rng.Next(2);                 // 1-2 quiz, gắn vào bài 1 và bài 3
            for (var q = 0; q < quizCount; q++)
            {
                var lessonNo = q == 0 ? 1 : 3;
                data.Quizzes.Add(new Quiz
                {
                    Key = $"q-{suffix}-{lessonNo:00}",
                    LessonKey = $"l-{suffix}-{lessonNo:00}",
                    Questions = Enumerable.Range(1, 3).Select(n => new QuizQuestion
                    {
                        Q = $"Câu {n} về {course.Title}?",
                        Options = new List<string> { "Đáp án A", "Đáp án B", "Đáp án C", "Đáp án D" },
                        AnswerIndex = _rng.Next(4)
                    }).ToList()
                });
            }
        }

        // ---- 3. Users theo 3 cụm persona: 40% dân data, 40% dân web, 20% dân infra ----
        // Cụm "dân data" học track data + lác đác be; "dân web" học fe + be; "dân infra" học infra.
        var clusterPools = new Dictionary<string, List<List<Course>>>
        {
            ["data"] = tracks.Where(t => t.Track is "data" or "be").Select(t => t.Courses).ToList(),
            ["web"]  = tracks.Where(t => t.Track is "fe" or "be").Select(t => t.Courses).ToList(),
            ["infra"]= tracks.Where(t => t.Track == "infra").Select(t => t.Courses).ToList(),
        };

        for (var i = 1; i <= scaleUsers; i++)
        {
            var cluster = i <= scaleUsers * 0.4 ? "data"
                        : i <= scaleUsers * 0.8 ? "web" : "infra";
            var user = new User
            {
                Key = $"u{i:000}",
                Name = $"{Ho[_rng.Next(Ho.Length)]} {Ten[_rng.Next(Ten.Length)]}",
                Email = $"user{i:000}@studygraph.dev",
                Role = i == 1 ? "admin" : "student",
                CreatedAt = Anchor.AddDays(-_rng.Next(90, 180)).ToString("o")
            };
            data.Users.Add(user);
            GenerateActivity(data, user, i, cluster, clusterPools[cluster]);
        }

        return data;
    }

    /// <summary>Ghi danh / hoàn thành / đánh giá cho 1 user theo persona của cụm.</summary>
    private void GenerateActivity(SeedData data, User user, int index, string cluster, List<List<Course>> pool)
    {
        var userId = $"users/{user.Key}";
        var mainTrack = pool[_rng.Next(pool.Count)];

        // Vài user đầu mỗi cụm là "người mới" — chỉ học khóa 101, dở dang:
        // đây chính là các user demo nhận gợi ý (kỳ vọng mục 4: học SQL 101
        // được gợi ý SQL 201, KHÔNG bị gợi ý React).
        var isFresh = index % 10 <= 1;

        var enrolled = isFresh
            ? new List<Course> { mainTrack[0] }
            : PickEnrollments(pool, 2 + _rng.Next(3));

        foreach (var course in enrolled)
        {
            var enrolledAt = Anchor.AddDays(-_rng.Next(10, 90));
            // % hoàn thành: người mới 30-70%, còn lại 30-100% (đủ user Progress=100 cho Q2)
            var lessonsDone = isFresh ? 1 + _rng.Next(3) : 1 + _rng.Next(5);
            var courseLessons = data.Lessons.Where(l => l.CourseKey == course.Key)
                                            .OrderBy(l => l.Order).Take(lessonsDone).ToList();

            data.EnrolledIn.Add(new EnrolledInEdge
            {
                From = userId,
                To = $"courses/{course.Key}",
                EnrolledAt = enrolledAt.ToString("o"),
                Progress = (int)Math.Round(100.0 * courseLessons.Count / 5)
            });

            foreach (var lesson in courseLessons)
            {
                var hasQuiz = data.Quizzes.Any(q => q.LessonKey == lesson.Key);
                data.Completed.Add(new CompletedEdge
                {
                    From = userId,
                    To = $"lessons/{lesson.Key}",
                    CompletedAt = enrolledAt.AddDays(_rng.Next(1, 30)).ToString("o"),
                    Score = hasQuiz ? 60 + _rng.Next(41) : null
                });
            }
        }

        // Rate 0-2 khóa: khóa cụm mình 4-5 sao; 20% rate 1 khóa cụm khác 1-3 sao
        foreach (var course in enrolled.OrderBy(_ => _rng.Next()).Take(_rng.Next(3)))
            data.Rated.Add(new RatedEdge
            {
                From = userId, To = $"courses/{course.Key}",
                Stars = 4 + _rng.Next(2), Comment = "Khóa học hữu ích"
            });

        if (_rng.NextDouble() < 0.2)
        {
            var foreign = data.Courses.Where(c => enrolled.All(e => e.Key != c.Key))
                                      .OrderBy(_ => _rng.Next()).FirstOrDefault();
            if (foreign is not null)
                data.Rated.Add(new RatedEdge
                {
                    From = userId, To = $"courses/{foreign.Key}",
                    Stars = 1 + _rng.Next(3), Comment = "Không hợp với mình"
                });
        }
    }

    /// <summary>Chọn 2-4 khóa trong pool của cụm, ưu tiên học từ Level thấp lên.</summary>
    private List<Course> PickEnrollments(List<List<Course>> pool, int count)
    {
        var result = new List<Course>();
        while (result.Count < count)
        {
            var track = pool[_rng.Next(pool.Count)];
            var next = track.FirstOrDefault(c => result.All(r => r.Key != c.Key));
            if (next is null) break;
            // đi theo chain: chỉ lấy khóa đầu tiên trong track chưa chọn
            result.Add(next);
        }
        return result;
    }

    /// <summary>
    /// 12 khóa gốc = 4 track. Khi scale (SF-vừa 200 khóa, SF-lớn 1.000 khóa — mục 7)
    /// nhân bản cấu trúc track (3 khóa/chain) để dữ liệu benchmark giữ nguyên hình dạng đồ thị.
    /// </summary>
    private List<(string Track, List<Course> Courses)> BuildTracks(int totalCourses, SeedData data)
    {
        var tracks = new List<(string, List<Course>)>();
        var trackCount = Math.Max(4, totalCourses / 3);

        for (var t = 0; t < trackCount; t++)
        {
            var b = BaseTracks[t % BaseTracks.Length];
            var gen = t / BaseTracks.Length;                  // thế hệ nhân bản: 0 = 12 khóa gốc
            var courses = new List<Course>();
            for (var lv = 0; lv < 3; lv++)
            {
                var key = gen == 0 ? b.Keys[lv] : $"{b.Keys[lv]}-g{gen}";
                courses.Add(new Course
                {
                    Key = key,
                    Title = gen == 0 ? b.Titles[lv] : $"{b.Titles[lv]} (bộ {gen})",
                    Category = b.Category,
                    Level = lv + 1,
                    Description = $"Khóa {b.Titles[lv]} thuộc track {b.Track}.",
                    Tags = new List<string> { b.Track, $"level{lv + 1}" }
                });
            }
            data.Courses.AddRange(courses);
            tracks.Add((b.Track, courses));

            // chain trong track: 101 → 201 → 301
            for (var lv = 0; lv < 2; lv++)
                data.PrerequisiteOf.Add(new PrerequisiteOfEdge
                {
                    From = $"courses/{courses[lv].Key}",
                    To = $"courses/{courses[lv + 1].Key}"
                });

            // Ca demo đẹp nhất cho Q2 (mục 4): c-aspnet-301 cần CẢ C# 201 lẫn SQL 101
            if (gen == 0 && b.Track == "be")
                data.PrerequisiteOf.Add(new PrerequisiteOfEdge
                {
                    From = "courses/c-sql-101",
                    To = $"courses/{courses[2].Key}"
                });
        }
        return tracks;
    }
}
