import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Title, Text, Group, Button } from '@mantine/core'
import { Link } from 'react-router-dom'
import { apiFetch, getToken } from './api.js'
import Layout from './components/Layout.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import AuthPage from './pages/AuthPage.jsx'
import ParentPage from './pages/ParentPage.jsx'
import AdminPage from './pages/AdminPage.jsx'

export default function App() {
  const [profile, setProfile] = useState(null)

  useEffect(() => {
    if (!getToken()) return
    apiFetch('/api/auth/me').then(setProfile).catch(() => setProfile(null))
  }, [])

  function withLayout(page) {
    return <Layout profile={profile} setProfile={setProfile}>{page}</Layout>
  }

  return (
    <Routes>
      <Route path="/login" element={<AuthPage mode="login" />} />
      <Route path="/register" element={<AuthPage mode="register" />} />
      <Route path="/parent" element={
        <ProtectedRoute profile={profile} role="Parent">{withLayout(<ParentPage />)}</ProtectedRoute>
      } />
      <Route path="/admin" element={
        <ProtectedRoute profile={profile} role="Admin">{withLayout(<AdminPage />)}</ProtectedRoute>
      } />
      <Route path="/" element={
        getToken()
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
