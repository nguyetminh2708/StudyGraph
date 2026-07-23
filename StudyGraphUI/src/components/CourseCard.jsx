import { Link } from 'react-router-dom'

const LEVELS = { 1: 'Cơ bản', 2: 'Trung cấp', 3: 'Nâng cao' }

export default function CourseCard({ course, reasons }) {
  return (
    <Link className="course-card" to={`/courses/${course.key}`}>
      <div className="card-top">
        <span className="badge">{course.category}</span>
        <span className="level">{LEVELS[course.level] ?? `Level ${course.level}`}</span>
      </div>
      <h2>{course.title}</h2>
      {course.description && <p className="desc">{course.description}</p>}
      {reasons?.length > 0 && (
        <ul className="reasons">
          {reasons.map((r) => (
            <li key={r}>{r}</li>
          ))}
        </ul>
      )}
    </Link>
  )
}
