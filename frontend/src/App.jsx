import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Title, Text, Group, Button, Center, Loader } from '@mantine/core'
import { Link } from 'react-router-dom'
import { apiFetch, getToken, setToken } from './api.js'
import Layout from './components/Layout.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import AuthPage from './pages/AuthPage.jsx'
import ParentPage from './pages/ParentPage.jsx'
import AdminPage from './pages/AdminPage.jsx'

export default function App() {
  const [token, setAuthToken] = useState(getToken())
  const [profile, setProfile] = useState(null)
  const [loading, setLoading] = useState(!!getToken())

  // Re-fetch the profile whenever the token changes (login, register, logout, or mount)
  useEffect(() => {
    if (!token) { setProfile(null); setLoading(false); return }
    let live = true
    setLoading(true)
    apiFetch('/api/auth/me')
      .then(p => { if (live) setProfile(p) })
      .catch(() => {
        if (!live) return
        setToken(null); setAuthToken(null); setProfile(null)
      })
      .finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [token])

  function onProfile(p) {
    setProfile(p)
    setLoading(false)
  }

  function onAuthenticated(newToken) {
    setToken(newToken)
    setAuthToken(newToken)   // triggers the effect above → profile loads → loading false
  }

  function logout() {
    setToken(null)
    setAuthToken(null)
    setProfile(null)
  }

  function withLayout(page) {
    return <Layout profile={profile} setProfile={setProfile} onLogout={logout}>{page}</Layout>
  }

  const gate = (role, page) => (
    <ProtectedRoute token={token} profile={profile} loading={loading} role={role}>
      {withLayout(page)}
    </ProtectedRoute>
  )

  if (loading) {
    return <Center h="100vh"><Loader color="blue" /></Center>
  }

  return (
    <Routes>
      <Route path="/login" element={<AuthPage key="login" mode="login" onAuthenticated={onAuthenticated} onProfile={onProfile} />} />
      <Route path="/register" element={<AuthPage key="register" mode="register" onAuthenticated={onAuthenticated} onProfile={onProfile} />} />
      <Route path="/parent" element={gate('Parent', <ParentPage />)} />
      <Route path="/admin" element={gate('Admin', <AdminPage />)} />
      <Route path="/" element={
        token
          ? <Navigate to={profile?.role === 'Admin' ? '/admin' : '/parent'} replace />
          : <div style={{ maxWidth: 520, margin: '60px auto', textAlign: 'center' }}>
              <Title order={1} mb="md">School Admission System</Title>
              <Text c="dimmed" mb="lg">First come, first served admission with time-limited seat holds.</Text>
              <Group justify="center"><Button component={Link} to="/login">Sign in</Button><Button component={Link} to="/register" variant="light">Register</Button></Group>
            </div>
      } />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
