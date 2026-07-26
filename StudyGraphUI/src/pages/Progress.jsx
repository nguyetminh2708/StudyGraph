import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get } from '../api'

export default function Progress() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false
    get('/api/user/progress')
      .then((r) => {
        if (!cancelled) setItems(r)
      })
      .catch((err) => {
        if (!cancelled) setError(err.message)
      })
    return () => {
      cancelled = true
    }
  }, [])

  if (error) return <p className="form-error">{error}</p>
  if (items == null) return <p className="muted">Đang tải…</p>

  return (
    <>
      <h1>Tiến độ của tôi</h1>
      {items.length === 0 && (
        <p className="muted">
          Bạn chưa ghi danh khóa nào — <Link to="/">chọn một khóa để bắt đầu</Link>.
        </p>
      )}
      <div className="progress-list">
        {items.map((item) => (
          <Link key={item.course.key} className="progress-row" to={`/courses/${item.course.key}`}>
            <div className="progress-row-head">
              <span className="badge">{item.course.category}</span>
              <b>{item.course.title}</b>
              <span className="muted">ghi danh {new Date(item.enrolledAt).toLocaleDateString('vi-VN')}</span>
              <span className="pct">{item.progress}%</span>
            </div>
            <div className="progress-track">
              <div className="progress-fill" style={{ width: `${item.progress}%` }} />
            </div>
          </Link>
        ))}
      </div>
    </>
  )
}
