import { Navigate, Outlet } from 'react-router-dom'
import { getToken } from '../api.js'

export default function ProtectedRoute({ profile, role, children }) {
  const token = getToken()
  if (!token) return <Navigate to="/login" replace />
  if (role && profile?.role !== role) return <Navigate to="/" replace />
  return children || <Outlet />
}
