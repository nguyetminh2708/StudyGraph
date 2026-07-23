import { useEffect, useState } from 'react'
import { get } from '../api'
import CourseCard from '../components/CourseCard'

const CATEGORIES = ['Tất cả', 'Database', 'Backend', 'Frontend', 'DevOps']

export default function Home() {
  const [courses, setCourses] = useState([])
  const [category, setCategory] = useState('Tất cả')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const selectCategory = (c) => {
    setCategory(c)
    setLoading(true)
    setError('')
  }

  useEffect(() => {
    let cancelled = false
    const query = category === 'Tất cả' ? '' : `&category=${category}`
    get(`/api/courses?pageSize=100${query}`)
      .then((data) => {
        if (!cancelled) setCourses(data.items)
      })
      .catch((err) => {
        if (!cancelled) setError(err.message)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [category])

  return (
    <>
      <h1>Khóa học</h1>
      <div className="filters">
        {CATEGORIES.map((c) => (
          <button
            key={c}
            type="button"
            className={c === category ? 'filter active' : 'filter'}
            onClick={() => selectCategory(c)}
          >
            {c}
          </button>
        ))}
      </div>
      {error && <p className="form-error">{error}</p>}
      {loading && <p className="muted">Đang tải…</p>}
      {!loading && !error && courses.length === 0 && <p className="muted">Chưa có khóa học nào.</p>}
      <div className="cards">
        {courses.map((c) => (
          <CourseCard key={c.key} course={c} />
        ))}
      </div>
    </>
  )
}
