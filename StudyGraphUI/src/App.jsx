import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { isLoggedIn } from './api'
import Layout from './components/Layout'
import Course from './pages/Course'
import Home from './pages/Home'
import Lesson from './pages/Lesson'
import Login from './pages/Login'
import Progress from './pages/Progress'
import './App.css'

const RequireAuth = () => (isLoggedIn() ? <Outlet /> : <Navigate to="/login" replace />)

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route element={<RequireAuth />}>
          <Route element={<Layout />}>
            <Route path="/" element={<Home />} />
            <Route path="/courses/:key" element={<Course />} />
            <Route path="/lessons/:key" element={<Lesson />} />
            <Route path="/me" element={<Progress />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
