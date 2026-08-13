import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { get, post } from '../api'

export default function Lesson() {
  const { key } = useParams()
  const navigate = useNavigate()
  const [detail, setDetail] = useState(null)
  const [quiz, setQuiz] = useState(null)
  const [siblings, setSiblings] = useState([])
  const [answers, setAnswers] = useState({})
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [showQuiz, setShowQuiz] = useState(false)

  const loaded = detail?.lesson.key === key

  useEffect(() => {
    let cancelled = false
    get(`/api/lessons/${key}`)
      .then(async (d) => {
        const [q, course] = await Promise.all([
          d.quizKey ? get(`/api/quizzes/${d.quizKey}`) : Promise.resolve(null),
          get(`/api/courses/${d.lesson.courseKey}`)
        ])
        if (cancelled) return
        setDetail(d)
        setQuiz(q)
        setSiblings(course.lessons)
        setAnswers({})
        setResult(null)
        setShowQuiz(false) // mở bài học luôn hiển thị nút "Hoàn thành bài học" trước, kể cả bài đã hoàn thành
        setError('')
        setMessage('')
      })
      .catch((err) => {
        if (!cancelled) setError(err.message)
      })
    return () => {
      cancelled = true
    }
  }, [key])

  const submitQuiz = async () => {
    setBusy(true)
    setMessage('')
    try {
      const body = { Answers: quiz.questions.map((_, i) => answers[i]) }
      setResult(await post(`/api/quizzes/${quiz.key}/submit`, body))
    } catch (err) {
      setMessage(err.message)
    } finally {
      setBusy(false)
    }
  }

  // Nhấn "Hoàn thành bài học":
  // - bài CÓ quiz  → mở quiz (phải đạt >= 80% mới được tính hoàn thành, backend xử lý)
  // - bài KHÔNG quiz → gọi API complete rồi chuyển thẳng sang bài tiếp theo
  //   (bài cuối thì quay về trang khóa học để xem tiến độ)
  const completeLesson = async () => {
    setMessage('')
    if (quiz) {
      setShowQuiz(true)
      return
    }
    setBusy(true)
    try {
      await post(`/api/lessons/${key}/complete`)
      const idx = siblings.findIndex((l) => l.key === key)
      const nextLesson = idx >= 0 && idx < siblings.length - 1 ? siblings[idx + 1] : null
      navigate(nextLesson ? `/lessons/${nextLesson.key}` : `/courses/${detail.lesson.courseKey}`)
    } catch (err) {
      setMessage(err.message)
    } finally {
      setBusy(false)
    }
  }

  if (error) return <p className="form-error">{error}</p>
  if (!loaded) return <p className="muted">Đang tải…</p>

  const { lesson } = detail
  const idx = siblings.findIndex((l) => l.key === key)
  const prev = idx > 0 ? siblings[idx - 1] : null
  const next = idx >= 0 && idx < siblings.length - 1 ? siblings[idx + 1] : null
  const justDone = result?.passed === true
  const allAnswered = quiz != null && quiz.questions.every((_, i) => answers[i] !== undefined)

  return (
    <>
      <div className="lesson-nav">
        <Link to={`/courses/${lesson.courseKey}`}>← Quay lại khóa học</Link>
        <span className="lesson-nav-links">
          {prev && <Link to={`/lessons/${prev.key}`}>← Bài {prev.order}</Link>}
          <span className="muted">
            Bài {lesson.order}/{siblings.length}
          </span>
          {next && <Link to={`/lessons/${next.key}`}>Bài {next.order} →</Link>}
        </span>
      </div>

      {/* Dữ liệu seed có title dạng "Bài 3: SQL căn bản" — chỉ thêm prefix khi title chưa có sẵn */}
      <h1>{/^bài\s*\d+/i.test(lesson.title) ? lesson.title : `Bài ${lesson.order}: ${lesson.title}`}</h1>

      {detail.completed && !justDone && (
        <p className="form-success">
          ✓ Bạn đã hoàn thành bài này{detail.score != null ? ` — điểm ${detail.score}/100` : ''}
          {quiz ? '. Làm lại quiz sẽ cập nhật điểm mới.' : '.'}
        </p>
      )}

      <p className="lesson-content">{lesson.content}</p>

      {!showQuiz && (
        <section className="section">
          <button type="button" onClick={completeLesson} disabled={busy}>
            {busy ? 'Đang lưu…' : 'Hoàn thành bài học'}
          </button>
          <p className="muted">
            {quiz
              ? 'Bài này có quiz — nhấn "Hoàn thành bài học" để làm quiz, đạt tối thiểu 80% mới qua bài tiếp theo.'
              : next
                ? 'Nhấn "Hoàn thành bài học" để lưu tiến độ và chuyển sang bài tiếp theo.'
                : 'Đây là bài cuối — nhấn "Hoàn thành bài học" để lưu tiến độ và quay lại khóa học.'}
          </p>
        </section>
      )}

      {quiz && showQuiz && (
        <section className="section">
          <h2>Quiz ({quiz.questions.length} câu)</h2>
          {quiz.questions.map((q, qi) => (
            <fieldset key={qi} className="quiz-question" disabled={result != null}>
              {/* Câu hỏi seed có sẵn "Câu N ..." — chỉ đánh số khi câu hỏi chưa tự đánh số */}
              <legend>{/^câu\s*\d+/i.test(q.q) ? q.q : `Câu ${qi + 1}: ${q.q}`}</legend>
              {q.options.map((opt, oi) => (
                <label key={oi} className="quiz-option">
                  <input
                    type="radio"
                    name={`q${qi}`}
                    checked={answers[qi] === oi}
                    onChange={() => setAnswers({ ...answers, [qi]: oi })}
                  />
                  {opt}
                </label>
              ))}
            </fieldset>
          ))}
          {result ? (
            result.passed ? (
              <p className="form-success">
                Đạt! Đúng {result.correct}/{result.total} — điểm {result.score}/100. Bài học đã tính hoàn thành, tiến độ
                khóa đã cập nhật 🎉
              </p>
            ) : (
              <>
                <p className="form-error">
                  Chưa đạt — đúng {result.correct}/{result.total}, điểm {result.score}/100 (cần tối thiểu 80). Xem lại
                  bài rồi thử lại nha!
                </p>
                <button
                  type="button"
                  onClick={() => {
                    setResult(null)
                    setAnswers({})
                  }}
                >
                  Làm lại quiz
                </button>
              </>
            )
          ) : (
            <button type="button" onClick={submitQuiz} disabled={!allAnswered || busy}>
              {busy ? 'Đang chấm…' : 'Nộp bài'}
            </button>
          )}
        </section>
      )}
      {message && <p className="form-error">{message}</p>}

      {justDone &&
        (next ? (
          <Link className="next-cta" to={`/lessons/${next.key}`}>
            Học bài tiếp theo: Bài {next.order} — {next.title} →
          </Link>
        ) : (
          <Link className="next-cta" to={`/courses/${lesson.courseKey}`}>
            Đây là bài cuối — quay lại khóa học xem tiến độ →
          </Link>
        ))}
    </>
  )
}
