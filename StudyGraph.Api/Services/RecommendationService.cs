using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Services
{

    /// <summary>
    /// RecommendationService: hợp nhất Q1 + Q2, mỗi gợi ý kèm lý do (mục 6).
    ///
    ///   score = (DuDieuKien ? 100 : 0)   // Q2: ưu tiên tuyệt đối khóa đã "mở khóa"
    ///         + SoBanHoc * 10            // Q1: mỗi bạn học chung +10
    ///         + (AvgStars ?? 0) * 2      // rating cộng nhẹ
    ///
    /// Phần Reason hiển thị trên UI — khi bảo vệ, giải thích được vì sao máy gợi ý
    /// ăn điểm hơn nhiều so với chỉ hiện danh sách.
    /// </summary>
    public class RecommendationService(
    RecommendationRepository recommendations,
    EnrollmentRepository enrollments)
    {
        public async Task<List<RecommendationItem>> GetForUserAsync(string userKey)
        {
            var userId = $"users/{userKey}";
            var myCourseIds = await enrollments.GetMyCourseIdsAsync(userId);
            var myCompletedCourseIds = await enrollments.GetMyCompletedCourseIdsAsync(userId);

            var collaborative = await recommendations.GetCollaborativeAsync(userId, myCourseIds);
            var unlocked = await recommendations.GetUnlockedAsync(myCourseIds, myCompletedCourseIds);

            // Trộn theo Course.Key — 1 khóa có thể xuất hiện ở cả Q1 lẫn Q2
            var merged = new Dictionary<string, (Course Course, int SoBanHoc, double? AvgStars, bool DuDieuKien)>();

            foreach (var s in collaborative)
                merged[s.Course.Key] = (s.Course, s.SoBanHoc, s.AvgStars, false);

            foreach (var u in unlocked)
            {
                if (merged.TryGetValue(u.Course.Key, out var existing))
                    merged[u.Course.Key] = (existing.Course, existing.SoBanHoc, existing.AvgStars, true);
                else
                    merged[u.Course.Key] = (u.Course, 0, null, true);
            }

            var result = new List<RecommendationItem>();
            foreach (var (course, soBanHoc, avgStars, duDieuKien) in merged.Values)
            {
                var score = (duDieuKien ? 100 : 0)
                          + soBanHoc * 10
                          + (avgStars ?? 0) * 2;

                var reasons = new List<string>();
                if (soBanHoc > 0)
                    reasons.Add($"{soBanHoc} người học cùng bạn đã học khóa này");
                if (duDieuKien)
                    reasons.Add("Bạn đã đủ điều kiện tiên quyết");
                if (avgStars is not null)
                    reasons.Add($"Điểm đánh giá trung bình {avgStars:0.0}/5");

                result.Add(new RecommendationItem
                {
                    CourseKey = course.Key,
                    Title = course.Title,
                    Category = course.Category,
                    Level = course.Level,
                    Score = Math.Round(score, 2),
                    Reasons = reasons
                });
            }

            return result.OrderByDescending(r => r.Score).ToList();
        }
    }
}
