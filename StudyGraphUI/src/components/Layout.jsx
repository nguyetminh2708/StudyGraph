import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { logout, userName } from '../api'

export default function Layout() {
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  return (
    <>
      <header className="topbar">
        <Link to="/" className="brand">
          StudyGraph
        </Link>
        <nav>
          <NavLink to="/" end>
            Khóa học
          </NavLink>
          <NavLink to="/me">Tiến độ</NavLink>
        </nav>
        <div className="userbox">
          <span>{userName()}</span>
          <button type="button" onClick={handleLogout}>
            Đăng xuất
          </button>
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
    </>
  )
}
