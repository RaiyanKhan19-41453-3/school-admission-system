import { useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { getToken, setToken, apiFetch } from '../api.js'

export default function Layout({ profile, setProfile, children }) {
  const navigate = useNavigate()
  const [err, setErr] = useState('')
  const token = getToken()

  async function logout() {
    try { await apiFetch('/api/auth/me') } catch { setProfile(null) }
    setToken(null); setProfile(null); navigate('/login')
  }

  return (
    <div className="app-shell">
      <nav className="topnav">
        <Link to="/" className="brand">🎓 School Admission</Link>
        <div className="nav-links">
          {token && profile?.role === 'Parent' && <NavLink to="/parent">Classes</NavLink>}
          {token && profile?.role === 'Admin' && <NavLink to="/admin">Admin</NavLink>}
        </div>
        <div className="nav-user">
          {token ? (
            <>
              <span className="muted">{profile?.fullName} <small>({profile?.role})</small></span>
              <button type="button" onClick={logout}>Sign out</button>
            </>
          ) : (
            <><Link to="/login">Sign in</Link><Link to="/register" className="btn-sm">Register</Link></>
          )}
        </div>
      </nav>
      {err && <div className="alert error">{err}</div>}
      <main className="main">{children}</main>
      <footer className="footer">School Admission System · Dhaka</footer>
    </div>
  )
}
