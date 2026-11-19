import { Navigate } from 'react-router-dom'
import { Center, Loader } from '@mantine/core'

export default function ProtectedRoute({ token, profile, loading, role, children }) {
  if (loading) return <Center h="100vh"><Loader color="blue" /></Center>
  if (!token) return <Navigate to="/login" replace />
  if (role && profile?.role !== role) return <Navigate to="/login" replace />
  return children
}
