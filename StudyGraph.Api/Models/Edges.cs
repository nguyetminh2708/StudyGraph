using Newtonsoft.Json;

namespace StudyGraph.Api.Models
{
    public class EnrolledInEdge
    {
        [JsonProperty("_from")] public string From { get; set; } = default!;  // users/u001
        [JsonProperty("_to")] public string To { get; set; } = default!;  // courses/c-sql-101
        public string EnrolledAt { get; set; } = default!;
        public int Progress { get; set; }
    }

    public class CompletedEdge
    {
        [JsonProperty("_from")] public string From { get; set; } = default!;  // users/u001
        [JsonProperty("_to")] public string To { get; set; } = default!;  // lessons/l-sql-101-01
        public string CompletedAt { get; set; } = default!;
        public int? Score { get; set; }                                        // điểm quiz (nếu có)
    }

    public class RatedEdge
    {
        [JsonProperty("_from")] public string From { get; set; } = default!;  // users/u001
        [JsonProperty("_to")] public string To { get; set; } = default!;  // courses/c-sql-101
        public int Stars { get; set; }                                         // 1..5
        public string? Comment { get; set; }
    }
}
