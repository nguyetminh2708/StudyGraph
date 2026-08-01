import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { del, get } from '../api'

export default function Admin() {
  const [stats, setStats] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const load = useCallback(() => {
    get('/api/admin/stats')
      .then(setStats)
      .catch((err) => setError(err.message))
  }, [])

  useEffect(() => {
    load()
  }, [load])

  const removeCourse = async (course) => {
    if (
      !window.confirm(`Xóa khóa "${course.title}"? Toàn bộ bài học, quiz và dữ liệu học liên quan sẽ bị xóa vĩnh viễn.`)
    )
      return
    setMessage('')
    try {
      await del(`/api/courses/${course.key}`)
      setMessage(`Đã xóa khóa "${course.title}"`)
      load()
    } catch (err) {
      setMessage(err.message)
    }
  }

  if (error) return <p className="form-error">{error}</p>
  if (stats == null) return <p className="muted">Đang tải…</p>

  const { totals, courseStats } = stats

  return (
    <>
      <div className="page-head">
        <h1>Quản trị</h1>
        <Link className="btn-like" to="/admin/courses/new">
          + Thêm khóa học
        </Link>
      </div>

      <div className="stats-grid">
        <div className="stat">
          <b>{totals.courses}</b>
          <span>Khóa học</span>
        </div>
        <div className="stat">
          <b>{totals.students}</b>
          <span>Học viên</span>
        </div>
        <div className="stat">
          <b>{totals.enrollments}</b>
          <span>Lượt ghi danh</span>
        </div>
        <div className="stat">
          <b>{totals.completions}</b>
          <span>Lượt hoàn thành bài</span>
        </div>
      </div>

      {message && <p className="muted">{message}</p>}

      <section className="section">
        <h2>Khóa học ({courseStats.length})</h2>
        <div className="table-scroll">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Khóa học</th>
                <th>Danh mục</th>
                <th>Trình độ</th>
                <th>Học viên</th>
                <th>Tiến độ TB</th>
                <th>Rating TB</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {courseStats.map((row) => (
                <tr key={row.course.key}>
                  <td>
                    <Link to={`/courses/${row.course.key}`}>{row.course.title}</Link>
                  </td>
                  <td>
                    <span className="badge">{row.course.category}</span>
                  </td>
                  <td>{row.course.level}</td>
                  <td>{row.students}</td>
                  <td>
                    <div className="cell-progress">
                      <div className="mini-track">
                        <div className="mini-fill" style={{ width: `${Math.round(row.avgProgress ?? 0)}%` }} />
                      </div>
                      <span>{Math.round(row.avgProgress ?? 0)}%</span>
                    </div>
                  </td>
                  <td>{row.avgStars != null ? `${row.avgStars.toFixed(1)} ★` : '—'}</td>
                  <td>
                    <div className="row-actions">
                      <Link className="btn-like small" to={`/admin/courses/${row.course.key}/edit`}>
                        Sửa
                      </Link>
                      <button type="button" className="danger small" onClick={() => removeCourse(row.course)}>
                        Xóa
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  )
}
