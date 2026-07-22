using StudyGraph.Api.Models;
using StudyGraph.Api.Repositories;

namespace StudyGraph.Api.Services;

public class QuizService(QuizRepository quizzes, EnrollmentRepository enrollments)
{
    /// <summary>Lấy đề — GIẤU AnswerIndex trước khi trả về client.</summary>
    public async Task<QuizView?> GetViewAsync(string quizKey)
    {
        var quiz = await quizzes.GetAsync(quizKey);
        if (quiz is null) return null;

        return new QuizView
        {
            Key = quiz.Key,
            LessonKey = quiz.LessonKey,
            Questions = quiz.Questions
                .Select(q => new QuizQuestionView { Q = q.Q, Options = q.Options })
                .ToList()
        };
    }

    /// <summary>
    /// Chấm điểm bài nộp, lưu Score vào edge completed của bài học tương ứng
    /// và cập nhật Progress trên enrolled_in.
    /// </summary>
    public async Task<QuizResult?> SubmitAsync(string userKey, string quizKey, QuizSubmission submission)
    {
        var quiz = await quizzes.GetAsync(quizKey);
        if (quiz is null) return null;

        var total = quiz.Questions.Count;
        var correct = 0;
        for (var i = 0; i < total && i < submission.Answers.Count; i++)
        {
            if (submission.Answers[i] == quiz.Questions[i].AnswerIndex)
                correct++;
        }

        var score = total == 0 ? 0 : (int)Math.Round(100.0 * correct / total);
        await enrollments.CompleteLessonAsync(userKey, quiz.LessonKey, score);

        return new QuizResult { Correct = correct, Total = total, Score = score };
    }
}
