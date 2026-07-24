import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { isLoggedIn } from './api'
import Layout from './components/Layout'
import Home from './pages/Home'
import Login from './pages/Login'
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
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
