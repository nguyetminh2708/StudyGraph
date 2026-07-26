import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { get, post } from '../api'

const LEVELS = { 1: 'Cơ bản', 2: 'Trung cấp', 3: 'Nâng cao' }

export default function Course() {
  const { key } = useParams()
  const [detail, setDetail] = useState(null)
  const [path, setPath] = useState([])
  const [enrollment, setEnrollment] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [enrolling, setEnrolling] = useState(false)

  const loaded = detail?.course.key === key

  useEffect(() => {
    let cancelled = false
    Promise.all([get(`/api/courses/${key}`), get(`/api/courses/${key}/learning-path`), get('/api/user/progress')])
      .then(([d, p, progress]) => {
        if (cancelled) return
        setDetail(d)
        setPath(p)
        setEnrollment(progress.find((item) => item.course.key === key) ?? null)
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

  const enroll = async () => {
    setEnrolling(true)
    setMessage('')
    try {
      const edge = await post(`/api/courses/${key}/enroll`)
      setEnrollment({ course: detail.course, progress: edge.progress ?? 0, enrolledAt: edge.enrolledAt })
      setMessage('Ghi danh thành công! Bắt đầu học thôi 🎉')
    } catch (err) {
      setMessage(err.message)
    } finally {
      setEnrolling(false)
    }
  }

  if (error) return <p className="form-error">{error}</p>
  if (!loaded) return <p className="muted">Đang tải…</p>

  const { course, lessons } = detail

  return (
    <>
      <div className="card-top">
        <span className="badge">{course.category}</span>
        <span className="level">{LEVELS[course.level] ?? `Level ${course.level}`}</span>
      </div>
      <h1>{course.title}</h1>
      {course.description && <p>{course.description}</p>}
      {course.tags?.length > 0 && <p className="muted">Tags: {course.tags.join(', ')}</p>}

      {enrollment ? (
        <div className="enroll-status">
          <span>Đã ghi danh — tiến độ {enrollment.progress}%</span>
          <div className="progress-track">
            <div className="progress-fill" style={{ width: `${enrollment.progress}%` }} />
          </div>
        </div>
      ) : (
        <button type="button" onClick={enroll} disabled={enrolling}>
          {enrolling ? 'Đang ghi danh…' : 'Ghi danh khóa này'}
        </button>
      )}
      {message && <p className={message.includes('thành công') ? 'form-success' : 'form-error'}>{message}</p>}

      {path.length > 0 && (
        <section className="section">
          <h2>Lộ trình — cần học trước</h2>
          <div className="path-chain">
            {path.map((step) => (
              <Link key={step.course.key} className="path-badge" to={`/courses/${step.course.key}`}>
                {step.course.title}
              </Link>
            ))}
            <span className="path-badge current">{course.title}</span>
          </div>
        </section>
      )}

      <section className="section">
        <h2>Bài học ({lessons.length})</h2>
        <ol className="lesson-list">
          {lessons.map((l, i) => {
            const done = detail.completedLessonKeys.includes(l.key)
            const unlocked = i === 0 || detail.completedLessonKeys.includes(lessons[i - 1].key)
            return (
              <li key={l.key}>
                {unlocked ? (
                  <Link to={`/lessons/${l.key}`}>{l.title}</Link>
                ) : (
                  <span className="locked">🔒 {l.title}</span>
                )}
                {done && <span className="tick">✓</span>}
              </li>
            )
          })}
        </ol>
        <p className="muted">Học tuần tự: hoàn thành bài trước để mở bài sau — bài có quiz cần đạt tối thiểu 80%.</p>
      </section>
    </>
  )
}
